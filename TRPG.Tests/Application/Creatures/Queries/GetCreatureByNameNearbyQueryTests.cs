using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureByNameNearbyQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private GetCreatureByNameNearbyQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreatureByNameNearbyQueryHandler(
            new GetCreatureByNameInRoomQueryHandler(_context),
            new GetCreatureByNameOutdoorsInDistrictQueryHandler(_context),
            new GetCreatureByNameOutdoorsInStateQueryHandler(_context)
        );
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsCreature_WhenIndoors()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var target = Builders.MakeCreature(WorldId, roomId: roomId);
        _context.Creatures.Add(target);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var player = Builders.MakeCreature(WorldId, roomId: roomId);

        // Act
        var result = await _handler.Handle(
            new GetCreatureByNameNearbyQuery
            {
                WorldId = WorldId,
                Player = player,
                Name = target.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(target.Id, result.Id);
    }

    [Fact]
    public async Task Handle_ScopesToDistrict_WhenOutdoorsInCity()
    {
        // Arrange
        var districtId = Guid.NewGuid();
        var target = Builders.MakeCreature(WorldId, districtId: districtId);
        _context.Creatures.Add(target);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var player = Builders.MakeCreature(WorldId, districtId: districtId);

        // Act
        var result = await _handler.Handle(
            new GetCreatureByNameNearbyQuery
            {
                WorldId = WorldId,
                Player = player,
                Name = target.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(target.Id, result.Id);
    }

    [Fact]
    public async Task Handle_ScopesToState_WhenOutdoorsWithNoCity()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var target = Builders.MakeCreature(WorldId, stateId: stateId);
        _context.Creatures.Add(target);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var player = Builders.MakeCreature(WorldId, stateId: stateId);

        // Act
        var result = await _handler.Handle(
            new GetCreatureByNameNearbyQuery
            {
                WorldId = WorldId,
                Player = player,
                Name = target.Name,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(target.Id, result.Id);
    }
}
