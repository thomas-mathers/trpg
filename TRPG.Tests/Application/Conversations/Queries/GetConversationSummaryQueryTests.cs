using TRPG.Application.Conversations.Queries;
using TRPG.Data;

namespace TRPG.Tests.Application.Conversations.Queries;

[Collection("Database")]
public sealed class GetConversationSummaryQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetConversationSummaryQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetConversationSummaryQueryHandler(_context);
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
            new GetConversationSummaryQuery { CreatureId = creatureId, NpcId = npcId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("", summary);
    }
}
