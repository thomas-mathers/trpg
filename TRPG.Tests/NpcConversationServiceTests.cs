using TRPG.Data;
using TRPG.Models;
using TRPG.Services;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class NpcConversationServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private NpcConversationService _service = null!;
    private readonly Guid _personId = Guid.NewGuid();
    private readonly Guid _npcId = Guid.NewGuid();

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _service = new NpcConversationService(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task AddMessage_CreatesConversationOnFirstMessage()
    {
        // Arrange & Act
        await _service.AddMessage(_personId, _npcId, "Hello", TestContext.Current.CancellationToken);

        // Assert
        var conversation = _context.NpcConversations
            .FirstOrDefault(c => c.PersonId == _personId && c.NpcId == _npcId);

        Assert.NotNull(conversation);
    }

    [Fact]
    public async Task AddMessage_ReusesSameConversationForSubsequentMessages()
    {
        // Arrange
        await _service.AddMessage(_personId, _npcId, "Hello", TestContext.Current.CancellationToken);

        // Act
        await _service.AddMessage(_personId, _npcId, "How are you?", TestContext.Current.CancellationToken);

        // Assert
        var conversations = _context.NpcConversations
            .Where(c => c.PersonId == _personId && c.NpcId == _npcId)
            .ToList();

        Assert.Single(conversations);
    }

    [Fact]
    public async Task AddMessage_AssignsIncrementingIndexes()
    {
        // Arrange & Act
        await _service.AddMessage(_personId, _npcId, "First", TestContext.Current.CancellationToken);
        await _service.AddMessage(_personId, _npcId, "Second", TestContext.Current.CancellationToken);
        await _service.AddMessage(_personId, _npcId, "Third", TestContext.Current.CancellationToken);

        // Assert
        var messages = await _service.GetAllMessages(_personId, _npcId, 0, TestContext.Current.CancellationToken);

        Assert.Equal(3, messages.Count);
        Assert.Equal(0, messages[0].Index);
        Assert.Equal(1, messages[1].Index);
        Assert.Equal(2, messages[2].Index);
    }

    [Fact]
    public async Task GetAllMessages_ReturnsEmpty_WhenNoConversation()
    {
        // Act
        var messages = await _service.GetAllMessages(Guid.NewGuid(), Guid.NewGuid(), 0, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(messages);
    }

    [Fact]
    public async Task GetAllMessages_FiltersToStartingIndex()
    {
        // Arrange
        await _service.AddMessage(_personId, _npcId, "Zero", TestContext.Current.CancellationToken);
        await _service.AddMessage(_personId, _npcId, "One", TestContext.Current.CancellationToken);
        await _service.AddMessage(_personId, _npcId, "Two", TestContext.Current.CancellationToken);

        // Act
        var messages = await _service.GetAllMessages(_personId, _npcId, 1, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, messages.Count);
        Assert.Equal("One", messages[0].Message);
        Assert.Equal("Two", messages[1].Message);
    }

    [Fact]
    public async Task UpdateSummary_SetsSummaryAndLastSummarizedIndex()
    {
        // Arrange
        await _service.AddMessage(_personId, _npcId, "Hello", TestContext.Current.CancellationToken);
        await _service.AddMessage(_personId, _npcId, "Goodbye", TestContext.Current.CancellationToken);

        // Act
        await _service.UpdateSummary(_personId, _npcId, "They greeted each other.", TestContext.Current.CancellationToken);

        // Assert
        var conversation = _context.NpcConversations
            .First(c => c.PersonId == _personId && c.NpcId == _npcId);

        Assert.Equal("They greeted each other.", conversation.Summary);
        Assert.Equal(1, conversation.LastSummarizedIndex);
    }

    [Fact]
    public async Task UpdateSummary_Throws_WhenNoConversation()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateSummary(Guid.NewGuid(), Guid.NewGuid(), "summary", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateSummary_Throws_WhenNoMessages()
    {
        // Arrange
        _context.NpcConversations.Add(new NpcConversation { PersonId = _personId, NpcId = _npcId });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateSummary(_personId, _npcId, "summary", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddMessage_WorksInBothDirections()
    {
        // Arrange & Act
        await _service.AddMessage(_personId, _npcId, "Player says hi", TestContext.Current.CancellationToken);
        await _service.AddMessage(_npcId, _personId, "NPC replies", TestContext.Current.CancellationToken);

        // Assert
        var messages = await _service.GetAllMessages(_personId, _npcId, 0, TestContext.Current.CancellationToken);
        Assert.Equal(2, messages.Count);
    }
}
