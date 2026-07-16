using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TRPG.Application.Common;
using TRPG.Application.Game.Commands;
using TRPG.Application.Game.Queries;

namespace TRPG.Application.Game;

public record TurnMetrics(string Response, long FirstTokenMs, long TotalMs, int TokenCount)
{
    public double TokensPerSecond =>
        TotalMs > FirstTokenMs ? TokenCount / ((TotalMs - FirstTokenMs) / 1000.0) : 0;
}

internal class GameTurnRunner(
    [FromKeyedServices(LlmRoleKeys.Gameplay)] IChatClient chatClient,
    GameTurnContext turnContext,
    GameSessionLocks sessionLocks,
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

        await using var @lock = await BeginTurn(cancellationToken);
        try
        {
            var inputOrdinal = await AppendUserMessage(@lock, openingPrompt, cancellationToken);
            await foreach (var token in StreamReply(@lock, cancellationToken))
            {
                yield return token;
            }

            await FinishTurn(@lock, inputOrdinal, result, cancellationToken);
        }
        finally
        {
            turnContext.Lock = null;
        }
    }

    public async IAsyncEnumerable<string> StreamWaitResponse(
        int hours,
        TurnStreamResult result,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await using var @lock = await BeginTurn(cancellationToken);
        try
        {
            await advanceTime.Handle(
                new AdvanceTimeCommand
                {
                    Lock = @lock,
                    Delta = hours * GameClock.RealTimePerInGameHour,
                },
                cancellationToken
            );

            var waitPrompt =
                $"{hours} hour(s) have passed. Call look now, then narrate the passage of time and the player's surroundings based on what it returns.";

            var inputOrdinal = await AppendUserMessage(@lock, waitPrompt, cancellationToken);
            await foreach (var token in StreamReply(@lock, cancellationToken))
            {
                yield return token;
            }

            await FinishTurn(@lock, inputOrdinal, result, cancellationToken);
        }
        finally
        {
            turnContext.Lock = null;
        }
    }

    public async Task<TurnMetrics> GetResponseWithMetrics(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        await using var @lock = await BeginTurn(cancellationToken);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            long? firstTokenElapsedMs = null;
            var tokenCount = 0;
            var buffer = new StringBuilder();

            var inputOrdinal = await AppendUserMessage(@lock, input, cancellationToken);
            await foreach (var token in StreamReply(@lock, cancellationToken))
            {
                firstTokenElapsedMs ??= stopwatch.ElapsedMilliseconds;
                tokenCount++;
                buffer.Append(token);
            }

            var totalMs = stopwatch.ElapsedMilliseconds;

            await FinishTurn(@lock, inputOrdinal, new TurnStreamResult(), cancellationToken);

            return new TurnMetrics(
                buffer.ToString(),
                firstTokenElapsedMs ?? totalMs,
                totalMs,
                tokenCount
            );
        }
        finally
        {
            turnContext.Lock = null;
        }
    }

    public async IAsyncEnumerable<string> StreamResponse(
        string input,
        TurnStreamResult result,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await using var @lock = await BeginTurn(cancellationToken);
        try
        {
            var inputOrdinal = await AppendUserMessage(@lock, input, cancellationToken);
            await foreach (var token in StreamReply(@lock, cancellationToken))
            {
                yield return token;
            }

            await FinishTurn(@lock, inputOrdinal, result, cancellationToken);
        }
        finally
        {
            turnContext.Lock = null;
        }
    }

    private async Task<GameSessionLock> BeginTurn(CancellationToken cancellationToken)
    {
        var @lock = await sessionLocks.Acquire(turnContext.SessionId, cancellationToken);
        try
        {
            turnContext.Lock = @lock;
            turnContext.DidMoveThisTurn = false;
            turnContext.DidSceneRefreshThisTurn = false;

            var snapshot = await getGameSession.Handle(
                new GetGameSessionQuery { Lock = @lock },
                cancellationToken
            );
            turnContext.WorldId = snapshot.WorldId;
            turnContext.PlayerId = snapshot.PlayerId;

            return @lock;
        }
        catch
        {
            turnContext.Lock = null;
            await @lock.DisposeAsync();
            throw;
        }
    }

    private async Task FinishTurn(
        GameSessionLock @lock,
        int currentTurnStart,
        TurnStreamResult result,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        if (turnContext.DidMoveThisTurn)
        {
            await CloseLingeringConversations(@lock, cancellationToken);
            await clearChatMessages.Handle(
                new ClearChatMessagesCommand { Lock = @lock, KeepFromOrdinal = currentTurnStart },
                cancellationToken
            );
        }

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { Lock = @lock },
            cancellationToken
        );
        result.DidSceneRefreshThisTurn = turnContext.DidSceneRefreshThisTurn;
        result.CurrentDate = GameClock.GetCurrentInGameDate(playtime);
        logger.LogInformation("[perf] FinishTurn took {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
    }

    private async Task<int> AppendUserMessage(
        GameSessionLock @lock,
        string input,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[game] >>> {Message}", input);
        var stopwatch = Stopwatch.StartNew();
        var inputOrdinal = await appendChatMessages.Handle(
            new AppendChatMessagesCommand
            {
                Lock = @lock,
                Messages = [new ChatMessage(ChatRole.User, input)],
            },
            cancellationToken
        );

        await advanceTime.Handle(
            new AdvanceTimeCommand
            {
                Lock = @lock,
                Delta = gameClockOptions.Value.RealTimePerMessage,
            },
            cancellationToken
        );
        logger.LogInformation(
            "[perf] AppendUserMessage (append + advance time) took {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds
        );

        return inputOrdinal;
    }

    private async IAsyncEnumerable<string> StreamReply(
        GameSessionLock @lock,
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
            new GetChatMessagesQuery { Lock = @lock },
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
        await appendChatMessages.Handle(
            new AppendChatMessagesCommand
            {
                Lock = @lock,
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

    private async Task ForceEndConversation(
        GameSessionLock @lock,
        string npcName,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var prompt =
            $"Before continuing, call end_conversation for {npcName} to save a summary of your conversation.";
        await AppendUserMessage(@lock, prompt, cancellationToken);
        await foreach (var _ in StreamReply(@lock, cancellationToken)) { }
        logger.LogInformation(
            "[perf] ForceEndConversation for {NpcName} took {ElapsedMs}ms",
            npcName,
            stopwatch.ElapsedMilliseconds
        );
    }

    private async Task CloseLingeringConversations(
        GameSessionLock @lock,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var openConversations = await getOpenConversations.Handle(
            new GetOpenConversationsQuery { Lock = @lock },
            cancellationToken
        );

        foreach (var npcName in openConversations.Keys)
        {
            await ForceEndConversation(@lock, npcName, cancellationToken);
        }

        var stillOpenConversations = await getOpenConversations.Handle(
            new GetOpenConversationsQuery { Lock = @lock },
            cancellationToken
        );

        foreach (var npcName in openConversations.Keys.Intersect(stillOpenConversations.Keys))
        {
            logger.LogWarning("[game] Failed to save conversation summary for {NpcName}", npcName);
        }

        await updateGameSession.Handle(
            new UpdateGameSessionCommand { Lock = @lock, OpenConversationCreatureIdsByName = [] },
            cancellationToken
        );

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
