using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetAllCreaturesInStateQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetAllCreaturesInStateQueryHandler _handler = null!;
    private readonly Guid _worldId = Guid.NewGuid();

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetAllCreaturesInStateQueryHandler(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyCreaturesInState()
    {
        // Arrange
        var stateId = Guid.NewGuid();

        var inState = Builders.MakeCreature(_worldId, stateId: stateId);
        var differentState = Builders.MakeCreature(_worldId);
        var otherWorld = Builders.MakeCreature(stateId: stateId);

        _context.Creatures.AddRange(inState, differentState, otherWorld);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var results = await _handler.Handle(
            new GetAllCreaturesInStateQuery { WorldId = _worldId, StateId = stateId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(results, p => p.Id == inState.Id);
        Assert.DoesNotContain(results, p => p.Id == differentState.Id);
        Assert.DoesNotContain(results, p => p.Id == otherWorld.Id);
    }
}
