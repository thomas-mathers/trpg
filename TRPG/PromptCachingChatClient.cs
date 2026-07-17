using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;

namespace TRPG;

internal sealed class PromptCachingChatClient(IChatClient innerClient)
    : DelegatingChatClient(innerClient)
{
    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var messageList = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();

        // Fixed breakpoint: caches tools + system prompt as one shared, long-lived unit.
        messageList[0].Contents[^1].WithCacheControl(Ttl.Ttl1h);

        // Rolling breakpoint: advances to the newest message every turn, so each
        // subsequent turn only pays full price for what's new since the last one.
        messageList[^1].Contents[^1].WithCacheControl(Ttl.Ttl5m);

        return base.GetStreamingResponseAsync(messageList, options, cancellationToken);
    }
}
