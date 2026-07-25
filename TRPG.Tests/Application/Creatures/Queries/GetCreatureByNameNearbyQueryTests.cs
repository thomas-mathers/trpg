using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureByNameNearbyQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetCreatureByNameNearbyQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetCreatureByNameNearbyQueryHandler>();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Creature> SeedTargetNear(Creature player)
    {
        var target = Builders.MakeCreature(
            WorldId,
            stateId: player.StateId,
            cityId: player.CityId,
            districtId: player.DistrictId,
            roomId: player.RoomId
        );
        _context.Creatures.Add(target);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return target;
    }

    [Fact]
    public async Task Handle_ReturnsCreature_WhenIndoors()
    {
        // Arrange
        var player = Builders.MakeCreature(WorldId, roomId: Guid.NewGuid());
        var target = await SeedTargetNear(player);

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
        var player = Builders.MakeCreature(WorldId, districtId: Guid.NewGuid());
        var target = await SeedTargetNear(player);

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
        var player = Builders.MakeCreature(WorldId, stateId: Guid.NewGuid());
        var target = await SeedTargetNear(player);

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
