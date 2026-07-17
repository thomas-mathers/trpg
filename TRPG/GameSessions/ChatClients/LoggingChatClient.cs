using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace TRPG.GameSessions.ChatClients;

internal sealed class LoggingChatClient(IChatClient innerClient, ILogger<LoggingChatClient> logger)
    : DelegatingChatClient(innerClient)
{
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var updateCount = 0;
        await foreach (
            var update in base.GetStreamingResponseAsync(messages, options, cancellationToken)
        )
        {
            updateCount++;
            yield return update;
        }

        logger.LogInformation(
            "[perf] Underlying model call took {ElapsedMs}ms, {UpdateCount} update(s)",
            stopwatch.ElapsedMilliseconds,
            updateCount
        );
    }
}
