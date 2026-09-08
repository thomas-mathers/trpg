using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Common.Llm;
using TRPG.GameSessions.ChatClients;

namespace TRPG.Tests.ChatClients;

public class PromptCachingChatClientTests
{
    private const string CacheControlKey = "anthropic:cache_control";

    private readonly CapturingChatClient _inner = new();

    [Fact]
    public async Task GetResponse_CachesUpToTheMarkedMessage_WhenACallerNamesItsPrefix()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "Rules."),
            Marked(new ChatMessage(ChatRole.User, "Title and prior pages.")),
            new(ChatRole.User, "Write page 3."),
        };

        // Act
        await Client()
            .GetResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(IsCached(_inner.Captured[1]));
        Assert.False(IsCached(_inner.Captured[2]));
    }

    [Fact]
    public async Task GetResponse_CachesTheOpeningAndTheTail_WhenNothingIsMarked()
    {
        // Arrange — a growing transcript cannot say where its stable part ends.
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "Rules."),
            new(ChatRole.User, "Hello."),
            new(ChatRole.Assistant, "Hello yourself."),
        };

        // Act
        await Client()
            .GetResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(IsCached(_inner.Captured[0]));
        Assert.True(IsCached(_inner.Captured[^1]));
        Assert.False(IsCached(_inner.Captured[1]));
    }

    [Fact]
    public async Task GetResponse_ClearsMarkersLeftOnReusedMessages()
    {
        // Arrange — the same message objects are handed back on the next page.
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "Rules."),
            Marked(new ChatMessage(ChatRole.User, "Title and prior pages.")),
            new(ChatRole.User, "Write page 3."),
        };
        var client = Client();
        await client.GetResponseAsync(
            messages,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Act
        await client.GetResponseAsync(
            messages,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Single(_inner.Captured.Where(IsCached));
    }

    private PromptCachingChatClient Client() =>
        new(_inner, NullLogger<PromptCachingChatClient>.Instance);

    private static ChatMessage Marked(ChatMessage message)
    {
        message.AdditionalProperties = new AdditionalPropertiesDictionary
        {
            [LlmCacheHints.PrefixEnd] = true,
        };
        return message;
    }

    private static bool IsCached(ChatMessage message) =>
        message.Contents.Any(content =>
            content.AdditionalProperties?.ContainsKey(CacheControlKey) == true
        );

    private sealed class CapturingChatClient : IChatClient
    {
        public List<ChatMessage> Captured { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            Captured.Clear();
            Captured.AddRange(messages);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Fine.")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
