using TRPG.Application.NpcConversations.Queries;
using TRPG.Data;

namespace TRPG.Tests.Application.NpcConversations.Queries;

[Collection("Database")]
public sealed class GetNpcConversationSummaryQueryTests(DatabaseFixture db) : IAsyncLifetime
{
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
        // Arrange
        var creatureId = Guid.NewGuid();
        var npcId = Guid.NewGuid();

        // Act
        var summary = await _handler.Handle(
            new GetNpcConversationSummaryQuery { CreatureId = creatureId, NpcId = npcId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("", summary);
    }
}
