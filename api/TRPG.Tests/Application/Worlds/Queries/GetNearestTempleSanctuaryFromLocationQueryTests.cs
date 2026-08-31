using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.WorldGeneration;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Queries;

[Collection("Database")]
public sealed class GetNearestTempleSanctuaryFromLocationQueryTests(DatabaseFixture db)
    : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetNearestTempleSanctuaryFromLocationQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetNearestTempleSanctuaryFromLocationQueryHandler>();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTheSanctuaryAndCityName_WhenATempleIsReachable()
    {
        // Arrange
        var sharedAnchor = Guid.NewGuid();
        var city = Builders.MakeCity(Guid.NewGuid(), Guid.NewGuid(), name: "Ashvale");
        var temple = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.Temple);
        var templeLocation = Builders.MakeLocation(
            WorldId,
            cityId: city.Id,
            id: temple.ExteriorLocationId
        );
        var sanctuaryLocation = Builders.MakeLocation(
            WorldId,
            coarseAnchorLocationId: sharedAnchor
        );
        var sanctuaryRoom = Builders.MakeRoom(
            temple.Id,
            worldId: WorldId,
            locationId: sanctuaryLocation.Id,
            name: TempleRoomNames.Sanctuary
        );
        var deathLocation = Builders.MakeLocation(WorldId, coarseAnchorLocationId: sharedAnchor);

        _context.Cities.Add(city);
        _context.Buildings.Add(temple);
        _context.Locations.AddRange(templeLocation, sanctuaryLocation, deathLocation);
        _context.Rooms.Add(sanctuaryRoom);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetNearestTempleSanctuaryFromLocationQuery
        {
            WorldId = WorldId,
            FromLocationId = deathLocation.Id,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(sanctuaryLocation.Id, result!.SanctuaryLocationId);
        Assert.Equal("Ashvale", result.CityName);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoTempleExistsInTheWorld()
    {
        // Arrange
        var deathLocation = Builders.MakeLocation(WorldId);
        _context.Locations.Add(deathLocation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetNearestTempleSanctuaryFromLocationQuery
        {
            WorldId = WorldId,
            FromLocationId = deathLocation.Id,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheOnlyTempleIsUnreachable()
    {
        // Arrange
        var city = Builders.MakeCity(Guid.NewGuid(), Guid.NewGuid());
        var temple = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.Temple);
        var templeLocation = Builders.MakeLocation(
            WorldId,
            cityId: city.Id,
            id: temple.ExteriorLocationId
        );
        var sanctuaryLocation = Builders.MakeLocation(
            WorldId,
            coarseAnchorLocationId: Guid.NewGuid()
        );
        var sanctuaryRoom = Builders.MakeRoom(
            temple.Id,
            worldId: WorldId,
            locationId: sanctuaryLocation.Id,
            name: TempleRoomNames.Sanctuary
        );
        var deathLocation = Builders.MakeLocation(WorldId, coarseAnchorLocationId: Guid.NewGuid());

        _context.Cities.Add(city);
        _context.Buildings.Add(temple);
        _context.Locations.AddRange(templeLocation, sanctuaryLocation, deathLocation);
        _context.Rooms.Add(sanctuaryRoom);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetNearestTempleSanctuaryFromLocationQuery
        {
            WorldId = WorldId,
            FromLocationId = deathLocation.Id,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }
}
