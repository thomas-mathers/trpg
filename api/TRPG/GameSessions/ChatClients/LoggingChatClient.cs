using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace TRPG.GameSessions.ChatClients;

internal sealed class LoggingChatClient(IChatClient innerClient, ILogger<LoggingChatClient> logger)
    : DelegatingChatClient(innerClient)
{
    private static long _nextCallId;

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var callId = Interlocked.Increment(ref _nextCallId);
        var stopwatch = Stopwatch.StartNew();
        long? firstTextMs = null;
        long? firstToolCallMs = null;
        long? previousUpdateMs = null;
        long maxInterUpdateGapMs = 0;
        var toolNames = new List<string>();
        var updates = new List<ChatResponseUpdate>();

        await foreach (
            var update in base.GetStreamingResponseAsync(messages, options, cancellationToken)
        )
        {
            updates.Add(update);
            var nowMs = stopwatch.ElapsedMilliseconds;
            if (previousUpdateMs is { } previousMs)
            {
                maxInterUpdateGapMs = Math.Max(maxInterUpdateGapMs, nowMs - previousMs);
            }

            previousUpdateMs = nowMs;

            if (!string.IsNullOrEmpty(update.Text))
            {
                firstTextMs ??= nowMs;
            }

            foreach (var content in update.Contents)
            {
                if (content is FunctionCallContent functionCall)
                {
                    firstToolCallMs ??= nowMs;
                    toolNames.Add(functionCall.Name);
                }
            }

            yield return update;
        }

        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var response = updates.ToChatResponse();
        var usage = response.Usage;
        var generationMs = firstTextMs is { } firstText ? elapsedMs - firstText : 0;
        var outputTokensPerSecond =
            usage?.OutputTokenCount is { } outputTokens && generationMs > 0
                ? outputTokens / (generationMs / 1000.0)
                : 0;

        logger.LogInformation(
            "[perf] Model call {CallId}: firstText={FirstTextMs}ms, firstToolCall={FirstToolCallMs}ms, total={TotalMs}ms, maxInterUpdateGap={MaxInterUpdateGapMs}ms, inputTokens={InputTokens}, cachedInputTokens={CachedInputTokens}, outputTokens={OutputTokens}, outputTokensPerSecond={OutputTokensPerSecond:F1}, finishReason={FinishReason}, toolCalls={ToolCalls}",
            callId,
            firstTextMs,
            firstToolCallMs,
            elapsedMs,
            maxInterUpdateGapMs,
            usage?.InputTokenCount,
            usage?.CachedInputTokenCount,
            usage?.OutputTokenCount,
            outputTokensPerSecond,
            response.FinishReason,
            toolNames.Count == 0 ? "none" : string.Join(",", toolNames)
        );
    }
}
