using TRPG.Data;
using TRPG.Models;
using TRPG.Services;

namespace TRPG.Tests;

[Collection("Database")]
public class NpcConversationServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private NpcConversationService _service = null!;

    public async Task InitializeAsync()
    {
        _context = db.CreateContext();
        _service = new NpcConversationService(_context);
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task AddMessage_CreatesConversationOnFirstMessage()
    {
        // Arrange
        var personId = Guid.NewGuid();
        var npcId = Guid.NewGuid();

        // Act
        await _service.AddMessage(personId, npcId, "Hello");

        // Assert
        var conversation = _context.NpcConversations
            .FirstOrDefault(c => c.PersonId == personId && c.NpcId == npcId);
        
        Assert.NotNull(conversation);
    }

    [Fact]
    public async Task AddMessage_ReusesSameConversationForSubsequentMessages()
    {
        // Arrange
        var personId = Guid.NewGuid();
        var npcId = Guid.NewGuid();
        await _service.AddMessage(personId, npcId, "Hello");

        // Act
        await _service.AddMessage(personId, npcId, "How are you?");

        // Assert
        var conversations = _context.NpcConversations
            .Where(c => c.PersonId == personId && c.NpcId == npcId)
            .ToList();
        
        Assert.Single(conversations);
    }

    [Fact]
    public async Task AddMessage_AssignsIncrementingIndexes()
    {
        // Arrange
        var personId = Guid.NewGuid();
        var npcId = Guid.NewGuid();

        // Act
        await _service.AddMessage(personId, npcId, "First");
        await _service.AddMessage(personId, npcId, "Second");
        await _service.AddMessage(personId, npcId, "Third");

        // Assert
        var messages = await _service.GetAllMessages(personId, npcId, 0);
        
        Assert.Equal(3, messages.Count);
        Assert.Equal(0, messages[0].Index);
        Assert.Equal(1, messages[1].Index);
        Assert.Equal(2, messages[2].Index);
    }

    [Fact]
    public async Task GetAllMessages_ReturnsEmpty_WhenNoConversation()
    {
        // Act
        var messages = await _service.GetAllMessages(Guid.NewGuid(), Guid.NewGuid(), 0);

        // Assert
        Assert.Empty(messages);
    }

    [Fact]
    public async Task GetAllMessages_FiltersToStartingIndex()
    {
        // Arrange
        var personId = Guid.NewGuid();
        var npcId = Guid.NewGuid();
        await _service.AddMessage(personId, npcId, "Zero");
        await _service.AddMessage(personId, npcId, "One");
        await _service.AddMessage(personId, npcId, "Two");

        // Act
        var messages = await _service.GetAllMessages(personId, npcId, 1);

        // Assert
        Assert.Equal(2, messages.Count);
        Assert.Equal("One", messages[0].Message);
        Assert.Equal("Two", messages[1].Message);
    }

    [Fact]
    public async Task UpdateSummary_SetsSummaryAndLastSummarizedIndex()
    {
        // Arrange
        var personId = Guid.NewGuid();
        var npcId = Guid.NewGuid();
        await _service.AddMessage(personId, npcId, "Hello");
        await _service.AddMessage(personId, npcId, "Goodbye");

        // Act
        await _service.UpdateSummary(personId, npcId, "They greeted each other.");

        // Assert
        var conversation = _context.NpcConversations
            .First(c => c.PersonId == personId && c.NpcId == npcId);
        
        Assert.Equal("They greeted each other.", conversation.Summary);
        Assert.Equal(1, conversation.LastSummarizedIndex);
    }

    [Fact]
    public async Task UpdateSummary_Throws_WhenNoConversation()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateSummary(Guid.NewGuid(), Guid.NewGuid(), "summary"));
    }

    [Fact]
    public async Task UpdateSummary_Throws_WhenNoMessages()
    {
        // Arrange
        var personId = Guid.NewGuid();
        var npcId = Guid.NewGuid();
        _context.NpcConversations.Add(new NpcConversation { PersonId = personId, NpcId = npcId });
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateSummary(personId, npcId, "summary"));
    }

    [Fact]
    public async Task AddMessage_WorksInBothDirections()
    {
        // Arrange
        var personId = Guid.NewGuid();
        var npcId = Guid.NewGuid();

        // Act
        await _service.AddMessage(personId, npcId, "Player says hi");
        await _service.AddMessage(npcId, personId, "NPC replies");

        // Assert
        var messages = await _service.GetAllMessages(personId, npcId, 0);
        Assert.Equal(2, messages.Count);
    }
}
