using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureByNameAtLocationQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private GetCreatureByNameAtLocationQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreatureByNameAtLocationQueryHandler(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsCreature_WhenAtLocation()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var creature = Builders.MakeCreature(WorldId, locationId: locationId);
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCreatureByNameAtLocationQuery
            {
                WorldId = WorldId,
                LocationId = locationId,
                Name = creature.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(creature.Id, result.Id);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenAtDifferentLocation()
    {
        // Arrange
        var creature = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCreatureByNameAtLocationQuery
            {
                WorldId = WorldId,
                LocationId = Guid.NewGuid(),
                Name = creature.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }
}
