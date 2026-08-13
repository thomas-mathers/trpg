using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TRPG.Application.Chat.Commands;
using TRPG.Application.Chat.Queries;
using TRPG.Application.Common;
using TRPG.Application.Configuration;
using TRPG.Application.GameTurns;

namespace TRPG.Application.Chat;

internal sealed record StreamedReply(int InputOrdinal, IAsyncEnumerable<string> Tokens);

internal class LlmConversationClient(
    [FromKeyedServices(LlmRoleKeys.Gameplay)] IChatClient chatClient,
    GameTurnContext turnContext,
    GetChatMessagesQueryHandler getChatMessages,
    AppendChatMessagesCommandHandler appendChatMessages,
    IEnumerable<AIFunction> tools,
    IOptionsMonitor<LlmRoleOptions> optionsMonitor,
    ILogger<LlmConversationClient> logger
)
{
    public async Task<StreamedReply> StreamReply(
        string input,
        bool includeTools,
        CancellationToken cancellationToken
    )
    {
        var inputOrdinal = await AppendUserMessage(input, cancellationToken);
        return new StreamedReply(
            inputOrdinal,
            StreamCompletionTokens(includeTools, cancellationToken)
        );
    }

    private async Task<int> AppendUserMessage(string input, CancellationToken cancellationToken)
    {
        logger.LogInformation("[game] >>> {Message}", input);

        var stopwatch = Stopwatch.StartNew();

        var inputOrdinal = await appendChatMessages.Handle(
            new AppendChatMessagesCommand
            {
                SessionId = turnContext.SessionId,
                Messages = [new ChatMessage(ChatRole.User, input)],
            },
            cancellationToken
        );

        logger.LogInformation(
            "[perf] AppendUserMessage took {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds
        );

        return inputOrdinal;
    }

    private async IAsyncEnumerable<string> StreamCompletionTokens(
        bool includeTools,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var gameplayOptions = optionsMonitor.Get(LlmRoleKeys.Gameplay);

        var additionalProperties = new AdditionalPropertiesDictionary { ["num_ctx"] = 8192 };

        if (gameplayOptions.Provider == LlmProvider.Ollama)
        {
            additionalProperties["think"] = gameplayOptions.Think ?? false;
        }

        var chatOptions = new ChatOptions
        {
            Tools = includeTools ? tools.Cast<AITool>().ToList() : [],
            Temperature = gameplayOptions.Temperature,
            AdditionalProperties = additionalProperties,
            Reasoning = gameplayOptions is { Provider: LlmProvider.Anthropic, Think: false }
                ? new ReasoningOptions { Effort = ReasoningEffort.None }
                : null,
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
}
