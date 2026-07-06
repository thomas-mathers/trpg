using TRPG.Data;
using TRPG.Services;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class NpcConversationServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private readonly Guid _creatureId = Guid.NewGuid();
    private readonly Guid _npcId = Guid.NewGuid();
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private NpcConversationService _service = null!;

    public ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new NpcConversationService(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetSummary_ReturnsEmpty_WhenNoConversation() {
        // Act
        var summary = await _service.GetSummary(_creatureId, _npcId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("", summary);
    }

    [Fact]
    public async Task SetSummary_CreatesConversationOnFirstCall() {
        // Act
        await _service.SetSummary(_worldId, _creatureId, _npcId, "They greeted each other.",
            TestContext.Current.CancellationToken);

        // Assert
        var summary = await _service.GetSummary(_creatureId, _npcId, TestContext.Current.CancellationToken);
        Assert.Equal("They greeted each other.", summary);
    }

    [Fact]
    public async Task SetSummary_OverwritesExistingSummary() {
        // Arrange
        await _service.SetSummary(_worldId, _creatureId, _npcId, "First summary.",
            TestContext.Current.CancellationToken);

        // Act
        await _service.SetSummary(_worldId, _creatureId, _npcId, "Second summary.",
            TestContext.Current.CancellationToken);

        // Assert
        var summary = await _service.GetSummary(_creatureId, _npcId, TestContext.Current.CancellationToken);
        Assert.Equal("Second summary.", summary);
    }

    [Fact]
    public async Task SetSummary_DoesNotCreateDuplicateConversations() {
        // Arrange
        await _service.SetSummary(_worldId, _creatureId, _npcId, "First summary.",
            TestContext.Current.CancellationToken);

        // Act
        await _service.SetSummary(_worldId, _creatureId, _npcId, "Second summary.",
            TestContext.Current.CancellationToken);

        // Assert
        var conversations = _context.NpcConversations
            .Where(c => c.CreatureId == _creatureId && c.NpcId == _npcId)
            .ToList();
        Assert.Single(conversations);
    }
}
