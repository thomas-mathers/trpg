using System.Runtime.CompilerServices;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace TRPG.GameSessions.ChatClients;

internal sealed class PromptCachingChatClient(
    IChatClient innerClient,
    ILogger<PromptCachingChatClient> logger
) : DelegatingChatClient(innerClient)
{
    private const string CacheControlKey = "anthropic:cache_control";

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var messageList = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();

        ClearCacheControl(messageList);

        messageList[0].Contents[^1].WithCacheControl(Ttl.Ttl1h);
        messageList[^1].Contents[^1].WithCacheControl(Ttl.Ttl5m);

        var enumerator = base.GetStreamingResponseAsync(messageList, options, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            ChatResponseUpdate update;
            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    yield break;
                }

                update = enumerator.Current;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCacheControlBlocks(messageList, ex);
                throw;
            }

            yield return update;
        }
    }

    private static void ClearCacheControl(IReadOnlyList<ChatMessage> messageList)
    {
        foreach (var content in messageList.SelectMany(m => m.Contents))
        {
            content.WithCacheControl((CacheControlEphemeral?)null);
        }
    }

    private void LogCacheControlBlocks(IReadOnlyList<ChatMessage> messageList, Exception ex)
    {
        var markedBlocks = messageList
            .SelectMany(
                (message, messageIndex) =>
                    message.Contents.Select(
                        (content, contentIndex) =>
                            (message.Role, messageIndex, contentIndex, content)
                    )
            )
            .Where(x => x.content.AdditionalProperties?.ContainsKey(CacheControlKey) == true)
            .Select(x =>
                $"[{x.messageIndex}].{x.Role}.Contents[{x.contentIndex}] ({x.content.GetType().Name})"
            )
            .ToArray();

        logger.LogError(
            ex,
            "[cache] Streaming call failed with {MessageCount} message(s) in the request and {MarkedBlockCount} cache_control block(s) marked: {MarkedBlocks}",
            messageList.Count,
            markedBlocks.Length,
            string.Join(", ", markedBlocks)
        );
    }
}
