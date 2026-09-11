using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using TRPG.Application.Books;
using TRPG.Application.Common.Llm;
using TRPG.Application.Worlds.Commands;
using BookSubjectType = TRPG.Domain.Models.BookSubjectType;
using BookTier = TRPG.Domain.Models.BookTier;
using BookWork = TRPG.Domain.Models.BookWork;
using Secret = TRPG.Domain.Models.Secret;

namespace TRPG.Tests.Application.Books;

public class BookPageComposerTests
{
    private static readonly BookWork Work = new()
    {
        WorldId = Guid.NewGuid(),
        Title = "The Ledger of the Ashen Hand",
        Tier = BookTier.Clue,
        SubjectType = BookSubjectType.Faction,
        SubjectName = "The Ashen Hand",
        PageCount = 5,
    };

    private readonly CapturingChatClient _client = new();

    [Fact]
    public async Task Compose_PutsThePriorPagesInTheCacheablePrefix()
    {
        // Arrange
        var request = new BookPageCompositionRequest(Work, 3, ["Page one.", "Page two."], null);

        // Act
        await _client.Compose(request);

        // Assert — page four's prompt opens with exactly this, so the provider can reuse it.
        var prefix = _client.Captured[1];
        Assert.Contains("Page one.", prefix.Text);
        Assert.Contains("Page two.", prefix.Text);
        Assert.True(prefix.AdditionalProperties?.ContainsKey(LlmCacheHints.PrefixEnd));
    }

    [Fact]
    public async Task Compose_KeepsThePageInstructionOutOfThePrefix()
    {
        // Arrange
        var request = new BookPageCompositionRequest(Work, 3, ["Page one."], null);

        // Act
        await _client.Compose(request);

        // Assert
        Assert.DoesNotContain("Write page 3", _client.Captured[1].Text);
        Assert.Contains("Write page 3", _client.Captured[2].Text);
        Assert.Null(_client.Captured[2].AdditionalProperties);
    }

    [Fact]
    public async Task Compose_KeepsASecretOutOfThePrefix()
    {
        // Arrange
        var secret = new Secret
        {
            WorldId = Work.WorldId,
            Subject = "the countersign of the Ashen Hand",
            Value = "ashes before dawn",
        };
        var request = new BookPageCompositionRequest(Work, 2, ["Page one."], secret);

        // Act
        await _client.Compose(request);

        // Assert — a secret inside the prefix would be resent on every later page of the book.
        Assert.DoesNotContain("ashes before dawn", _client.Captured[1].Text);
        Assert.Contains("ashes before dawn", _client.Captured[2].Text);
    }

    [Fact]
    public async Task Compose_SaysNothingIsWrittenYet_OnTheOpeningPage()
    {
        // Arrange
        var request = new BookPageCompositionRequest(Work, 1, [], null);

        // Act
        await _client.Compose(request);

        // Assert
        Assert.Contains("No pages have been written yet.", _client.Captured[1].Text);
    }

    [Fact]
    public async Task Compose_GroundsTheAuthorInActualRooms_WhenWritingAnExpeditionJournal()
    {
        // Arrange
        var context = new ExpeditionJournalContext(
            Author: "Mara",
            DungeonHistory: "An abandoned mine.",
            Purpose: "Survey the workings.",
            Separation: "Parted at the storeroom.",
            FinalExperience: "Reached the study after a fall.",
            Route: ["Entrance", "Storeroom", "Study"]
        );
        var request = new BookPageCompositionRequest(Work, 1, [], null, context);

        // Act
        await _client.Compose(request);

        // Assert
        Assert.Contains("Mara", _client.Captured[1].Text, StringComparison.Ordinal);
        Assert.Contains(
            "Entrance → Storeroom → Study",
            _client.Captured[1].Text,
            StringComparison.Ordinal
        );
        Assert.Contains("An abandoned mine.", _client.Captured[1].Text, StringComparison.Ordinal);
        Assert.Contains(
            "Do not describe the author's own death",
            _client.Captured[1].Text,
            StringComparison.Ordinal
        );
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public List<ChatMessage> Captured { get; } = [];

        public Task<string> Compose(BookPageCompositionRequest request) =>
            new BookPageComposer(this).Compose(request, TestContext.Current.CancellationToken);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            Captured.Clear();
            Captured.AddRange(messages);
            return Task.FromResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "A page."))
            );
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
