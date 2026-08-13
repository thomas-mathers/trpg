using TRPG.Application.NpcConversations.Commands;
using TRPG.Application.NpcConversations.Queries;
using TRPG.Data;

namespace TRPG.Tests.Application.NpcConversations.Commands;

[Collection("Database")]
public sealed class SetNpcConversationSummaryCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid CreatureId = Guid.NewGuid();
    private static readonly Guid NpcId = Guid.NewGuid();
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private GetNpcConversationSummaryQueryHandler _getSummary = null!;
    private SetNpcConversationSummaryCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new SetNpcConversationSummaryCommandHandler(_context);
        _getSummary = new GetNpcConversationSummaryQueryHandler(_context);
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
            new SetNpcConversationSummaryCommand
            {
                WorldId = WorldId,
                CreatureId = CreatureId,
                NpcId = NpcId,
                Summary = "They greeted each other.",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var summary = await _getSummary.Handle(
            new GetNpcConversationSummaryQuery { CreatureId = CreatureId, NpcId = NpcId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal("They greeted each other.", summary);
    }

    [Fact]
    public async Task Handle_OverwritesExistingSummary()
    {
        // Arrange
        await _handler.Handle(
            new SetNpcConversationSummaryCommand
            {
                WorldId = WorldId,
                CreatureId = CreatureId,
                NpcId = NpcId,
                Summary = "First summary.",
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SetNpcConversationSummaryCommand
            {
                WorldId = WorldId,
                CreatureId = CreatureId,
                NpcId = NpcId,
                Summary = "Second summary.",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var summary = await _getSummary.Handle(
            new GetNpcConversationSummaryQuery { CreatureId = CreatureId, NpcId = NpcId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal("Second summary.", summary);
    }

    [Fact]
    public async Task Handle_DoesNotCreateDuplicateConversations()
    {
        // Arrange
        await _handler.Handle(
            new SetNpcConversationSummaryCommand
            {
                WorldId = WorldId,
                CreatureId = CreatureId,
                NpcId = NpcId,
                Summary = "First summary.",
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SetNpcConversationSummaryCommand
            {
                WorldId = WorldId,
                CreatureId = CreatureId,
                NpcId = NpcId,
                Summary = "Second summary.",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var conversations = _context
            .NpcConversations.Where(c => c.CreatureId == CreatureId && c.NpcId == NpcId)
            .ToList();
        Assert.Single(conversations);
    }
}
