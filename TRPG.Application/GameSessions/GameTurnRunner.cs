using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TRPG.Application.Common;
using TRPG.Application.Configuration;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data;

namespace TRPG.Application.GameSessions;

public record TurnMetrics(string Response, long FirstTokenMs, long TotalMs, int TokenCount)
{
    public double TokensPerSecond =>
        TotalMs > FirstTokenMs ? TokenCount / ((TotalMs - FirstTokenMs) / 1000.0) : 0;
}

internal class GameTurnRunner(
    [FromKeyedServices(LlmRoleKeys.Gameplay)] IChatClient chatClient,
    TrpgDbContext context,
    GameTurnContext turnContext,
    GetGameSessionQueryHandler getGameSession,
    GetOpenConversationsQueryHandler getOpenConversations,
    GetPlaytimeQueryHandler getPlaytime,
    AdvanceTimeCommandHandler advanceTime,
    UpdateGameSessionCommandHandler updateGameSession,
    GetChatMessagesQueryHandler getChatMessages,
    AppendChatMessagesCommandHandler appendChatMessages,
    ClearChatMessagesCommandHandler clearChatMessages,
    IEnumerable<AIFunction> tools,
    IOptionsMonitor<LlmRoleOptions> optionsMonitor,
    IOptionsSnapshot<GameClockOptions> gameClockOptions,
    ILogger<GameTurnRunner> logger
)
{
    public async IAsyncEnumerable<string> StreamOpeningResponse(
        TurnStreamResult result,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        const string openingPrompt =
            "This is the start of the session. Call look now, then narrate the opening scene based on what it returns.";

        using var _ = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["SessionId"] = turnContext.SessionId,
                ["TurnId"] = Guid.NewGuid().ToString("N")[..8],
            }
        );

        await BeginTurn(cancellationToken);

        var inputOrdinal = await AppendUserMessage(openingPrompt, cancellationToken);
        await foreach (var token in StreamReply(cancellationToken))
        {
            yield return token;
        }

        await FinishTurn(inputOrdinal, result, cancellationToken);
    }

    public async IAsyncEnumerable<string> StreamWaitResponse(
        int hours,
        TurnStreamResult result,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        using var _ = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["SessionId"] = turnContext.SessionId,
                ["TurnId"] = Guid.NewGuid().ToString("N")[..8],
            }
        );

        await BeginTurn(cancellationToken);

        await advanceTime.Handle(
            new AdvanceTimeCommand
            {
                SessionId = turnContext.SessionId,
                Delta = hours * GameClock.RealTimePerInGameHour,
            },
            cancellationToken
        );

        var waitPrompt =
            $"{hours} hour(s) have passed. Call look now, then narrate the passage of time and the player's surroundings based on what it returns.";

        var inputOrdinal = await AppendUserMessage(waitPrompt, cancellationToken);
        await foreach (var token in StreamReply(cancellationToken))
        {
            yield return token;
        }

        await FinishTurn(inputOrdinal, result, cancellationToken);
    }

    public async Task<TurnMetrics> GetResponseWithMetrics(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["SessionId"] = turnContext.SessionId,
                ["TurnId"] = Guid.NewGuid().ToString("N")[..8],
            }
        );

        await BeginTurn(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        long? firstTokenElapsedMs = null;
        var tokenCount = 0;
        var buffer = new StringBuilder();

        var inputOrdinal = await AppendUserMessage(input, cancellationToken);
        await foreach (var token in StreamReply(cancellationToken))
        {
            firstTokenElapsedMs ??= stopwatch.ElapsedMilliseconds;
            tokenCount++;
            buffer.Append(token);
        }

        var totalMs = stopwatch.ElapsedMilliseconds;

        await FinishTurn(inputOrdinal, new TurnStreamResult(), cancellationToken);

        return new TurnMetrics(
            buffer.ToString(),
            firstTokenElapsedMs ?? totalMs,
            totalMs,
            tokenCount
        );
    }

    public async IAsyncEnumerable<string> StreamResponse(
        string input,
        TurnStreamResult result,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        using var _ = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["SessionId"] = turnContext.SessionId,
                ["TurnId"] = Guid.NewGuid().ToString("N")[..8],
            }
        );

        await BeginTurn(cancellationToken);

        var inputOrdinal = await AppendUserMessage(input, cancellationToken);
        await foreach (var token in StreamReply(cancellationToken))
        {
            yield return token;
        }

        await FinishTurn(inputOrdinal, result, cancellationToken);
    }

    private async Task BeginTurn(CancellationToken cancellationToken)
    {
        turnContext.DidMoveThisTurn = false;
        turnContext.DidSceneRefreshThisTurn = false;

        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );
        turnContext.WorldId = snapshot.WorldId;
        turnContext.PlayerId = snapshot.PlayerId;
    }

    private async Task FinishTurn(
        int currentTurnStart,
        TurnStreamResult result,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        if (turnContext.DidMoveThisTurn)
        {
            await CloseLingeringConversations(currentTurnStart, cancellationToken);
        }

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );
        result.DidSceneRefreshThisTurn = turnContext.DidSceneRefreshThisTurn;
        result.CurrentDate = GameClock.GetCurrentInGameDate(playtime);
        logger.LogInformation(
            "[perf] FinishTurn took {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds
        );
    }

    private async Task<int> AppendUserMessage(string input, CancellationToken cancellationToken)
    {
        logger.LogInformation("[game] >>> {Message}", input);
        var stopwatch = Stopwatch.StartNew();
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );

        var inputOrdinal = await appendChatMessages.Handle(
            new AppendChatMessagesCommand
            {
                SessionId = turnContext.SessionId,
                Messages = [new ChatMessage(ChatRole.User, input)],
            },
            cancellationToken
        );

        await advanceTime.Handle(
            new AdvanceTimeCommand
            {
                SessionId = turnContext.SessionId,
                Delta = gameClockOptions.Value.RealTimePerMessage,
            },
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "[perf] AppendUserMessage (append + advance time) took {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds
        );

        return inputOrdinal;
    }

    private async IAsyncEnumerable<string> StreamReply(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var gameplayOptions = optionsMonitor.Get(LlmRoleKeys.Gameplay);
        var chatOptions = new ChatOptions
        {
            Tools = tools.Cast<AITool>().ToList(),
            Temperature = gameplayOptions.Temperature,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["think"] = gameplayOptions.Think ?? false,
                ["num_ctx"] = 8192,
            },
        };

        var messages = await getChatMessages.Handle(
            new GetChatMessagesQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );

        var stopwatch = Stopwatch.StartNew();
        long? firstTokenElapsedMs = null;
        var tokenCount = 0;
        var updates = new List<ChatResponseUpdate>();
        var thinking = new StringBuilder();
        await foreach (
            var update in chatClient.GetStreamingResponseAsync(
                messages,
                chatOptions,
                cancellationToken
            )
        )
        {
            updates.Add(update);
            foreach (var content in update.Contents)
            {
                if (content is TextReasoningContent reasoning)
                {
                    thinking.Append(reasoning.Text);
                }
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                firstTokenElapsedMs ??= stopwatch.ElapsedMilliseconds;
                tokenCount++;
                yield return update.Text;
            }
        }

        var totalMs = stopwatch.ElapsedMilliseconds;
        logger.LogInformation(
            "[perf] LLM response first token after {FirstTokenMs}ms, total {TotalMs}ms, {TokenCount} tokens",
            firstTokenElapsedMs ?? totalMs,
            totalMs,
            tokenCount
        );

        var aggregated = updates.ToChatResponse();
        if (aggregated.Usage is { } usage)
        {
            logger.LogInformation(
                "[perf] Usage input={InputTokens} cachedInput={CachedInputTokens} output={OutputTokens} additional={AdditionalCounts}",
                usage.InputTokenCount,
                usage.CachedInputTokenCount,
                usage.OutputTokenCount,
                usage.AdditionalCounts is null
                    ? ""
                    : string.Join(
                        ", ",
                        usage.AdditionalCounts.Select(pair => $"{pair.Key}={pair.Value}")
                    )
            );
        }

        await appendChatMessages.Handle(
            new AppendChatMessagesCommand
            {
                SessionId = turnContext.SessionId,
                Messages = aggregated.Messages.ToArray(),
            },
            cancellationToken
        );

        if (thinking.Length > 0)
        {
            logger.LogDebug("[game] think: {Thinking}", thinking.ToString().Trim());
        }

        logger.LogInformation("[game] <<< {Response}", aggregated.Text);
    }

    private async Task ForceEndConversation(string npcName, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var prompt =
            $"Before continuing, call end_conversation for {npcName} to save a summary of your conversation.";
        await AppendUserMessage(prompt, cancellationToken);
        await foreach (var _ in StreamReply(cancellationToken)) { }
        logger.LogInformation(
            "[perf] ForceEndConversation for {NpcName} took {ElapsedMs}ms",
            npcName,
            stopwatch.ElapsedMilliseconds
        );
    }

    private async Task CloseLingeringConversations(
        int currentTurnStart,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var openConversations = await getOpenConversations.Handle(
            new GetOpenConversationsQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );

        foreach (var npcName in openConversations.Keys)
        {
            await ForceEndConversation(npcName, cancellationToken);
        }

        var stillOpenConversations = await getOpenConversations.Handle(
            new GetOpenConversationsQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );

        foreach (var npcName in openConversations.Keys.Intersect(stillOpenConversations.Keys))
        {
            logger.LogWarning("[game] Failed to save conversation summary for {NpcName}", npcName);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );

        await updateGameSession.Handle(
            new UpdateGameSessionCommand
            {
                SessionId = turnContext.SessionId,
                OpenConversationCreatureIdsByName = [],
            },
            cancellationToken
        );

        await clearChatMessages.Handle(
            new ClearChatMessagesCommand
            {
                SessionId = turnContext.SessionId,
                KeepFromOrdinal = currentTurnStart,
            },
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        if (openConversations.Count > 0)
        {
            logger.LogInformation(
                "[perf] CloseLingeringConversations closed {Count} conversation(s) in {ElapsedMs}ms",
                openConversations.Count,
                stopwatch.ElapsedMilliseconds
            );
        }
    }
}
