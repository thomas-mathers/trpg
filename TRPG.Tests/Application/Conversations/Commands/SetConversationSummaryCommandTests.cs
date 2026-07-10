using TRPG.Application.Conversations.Commands;
using TRPG.Application.Conversations.Queries;
using TRPG.Data;

namespace TRPG.Tests.Application.Conversations.Commands;

[Collection("Database")]
public sealed class SetConversationSummaryCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly Guid _creatureId = Guid.NewGuid();
    private readonly Guid _npcId = Guid.NewGuid();
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private GetConversationSummaryQueryHandler _getSummary = null!;
    private SetConversationSummaryCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new SetConversationSummaryCommandHandler(_context);
        _getSummary = new GetConversationSummaryQueryHandler(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CreatesConversationOnFirstCall()
    {
        // Act
        await _handler.Handle(
            new SetConversationSummaryCommand
            {
                WorldId = _worldId,
                CreatureId = _creatureId,
                NpcId = _npcId,
                Summary = "They greeted each other.",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var summary = await _getSummary.Handle(
            new GetConversationSummaryQuery { CreatureId = _creatureId, NpcId = _npcId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal("They greeted each other.", summary);
    }

    [Fact]
    public async Task Handle_OverwritesExistingSummary()
    {
        // Arrange
        await _handler.Handle(
            new SetConversationSummaryCommand
            {
                WorldId = _worldId,
                CreatureId = _creatureId,
                NpcId = _npcId,
                Summary = "First summary.",
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SetConversationSummaryCommand
            {
                WorldId = _worldId,
                CreatureId = _creatureId,
                NpcId = _npcId,
                Summary = "Second summary.",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var summary = await _getSummary.Handle(
            new GetConversationSummaryQuery { CreatureId = _creatureId, NpcId = _npcId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal("Second summary.", summary);
    }

    [Fact]
    public async Task Handle_DoesNotCreateDuplicateConversations()
    {
        // Arrange
        await _handler.Handle(
            new SetConversationSummaryCommand
            {
                WorldId = _worldId,
                CreatureId = _creatureId,
                NpcId = _npcId,
                Summary = "First summary.",
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SetConversationSummaryCommand
            {
                WorldId = _worldId,
                CreatureId = _creatureId,
                NpcId = _npcId,
                Summary = "Second summary.",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var conversations = _context
            .NpcConversations.Where(c => c.CreatureId == _creatureId && c.NpcId == _npcId)
            .ToList();
        Assert.Single(conversations);
    }
}
