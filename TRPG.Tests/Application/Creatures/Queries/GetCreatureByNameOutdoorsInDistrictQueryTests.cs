using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureByNameOutdoorsInDistrictQueryTests(DatabaseFixture db)
    : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private GetCreatureByNameOutdoorsInDistrictQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreatureByNameOutdoorsInDistrictQueryHandler(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsCreature_WhenInDistrict()
    {
        // Arrange
        var districtId = Guid.NewGuid();
        var creature = Builders.MakeCreature(WorldId, districtId: districtId);
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCreatureByNameOutdoorsInDistrictQuery
            {
                WorldId = WorldId,
                DistrictId = districtId,
                Name = creature.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(creature.Id, result.Id);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenInDifferentDistrict()
    {
        // Arrange
        var creature = Builders.MakeCreature(WorldId, districtId: Guid.NewGuid());
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCreatureByNameOutdoorsInDistrictQuery
            {
                WorldId = WorldId,
                DistrictId = Guid.NewGuid(),
                Name = creature.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }
}
