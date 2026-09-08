using System.Runtime.CompilerServices;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Llm;

namespace TRPG.GameSessions.ChatClients;

internal sealed class PromptCachingChatClient(
    IChatClient innerClient,
    ILogger<PromptCachingChatClient> logger
) : DelegatingChatClient(innerClient)
{
    private const string CacheControlKey = "anthropic:cache_control";

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var messageList = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();

        ClearCacheControl(messageList);
        MarkCacheableContent(messageList);

        try
        {
            return await base.GetResponseAsync(messageList, options, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCacheControlBlocks(messageList, ex);
            throw;
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var messageList = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();

        ClearCacheControl(messageList);
        MarkCacheableContent(messageList);

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

    // A caller that knows where its stable prefix ends says so; a growing transcript does not, so
    // it falls back to the opening instructions and whatever was said last.
    private static void MarkCacheableContent(IReadOnlyList<ChatMessage> messageList)
    {
        var prefixEnd = messageList.LastOrDefault(message =>
            message.AdditionalProperties?.ContainsKey(LlmCacheHints.PrefixEnd) == true
        );
        if (prefixEnd != null)
        {
            prefixEnd.Contents[^1].WithCacheControl(Ttl.Ttl5m);
            return;
        }

        messageList[0].Contents[^1].WithCacheControl(Ttl.Ttl1h);
        messageList[^1].Contents[^1].WithCacheControl(Ttl.Ttl5m);
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
            "[cache] Call failed with {MessageCount} message(s) in the request and {MarkedBlockCount} cache_control block(s) marked: {MarkedBlocks}",
            messageList.Count,
            markedBlocks.Length,
            string.Join(", ", markedBlocks)
        );
    }
}
