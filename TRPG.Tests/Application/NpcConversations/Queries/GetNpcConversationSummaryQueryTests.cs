using TRPG.Application.NpcConversations.Queries;
using TRPG.Data;

namespace TRPG.Tests.Application.NpcConversations.Queries;

[Collection("Database")]
public sealed class GetNpcConversationSummaryQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly Guid _creatureId = Guid.NewGuid();
    private readonly Guid _npcId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private GetNpcConversationSummaryQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetNpcConversationSummaryQueryHandler(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoConversation()
    {
        // Act
        var summary = await _handler.Handle(
            new GetNpcConversationSummaryQuery { CreatureId = _creatureId, NpcId = _npcId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("", summary);
    }
}
