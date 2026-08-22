using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Buildings;
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
    public async Task Handle_ReturnsTheSanctuary_WhenTheDeathCityHasATemple()
    {
        // Arrange
        var city = await SeedCityWithTemple("Ashvale");
        var deathDistrict = Builders.MakeDistrict(city.CityId, DistrictType.Residential, WorldId);
        var deathLocation = Builders.MakeLocation(
            WorldId,
            stateId: city.StateId,
            cityId: city.CityId,
            districtId: deathDistrict.Id,
            id: deathDistrict.LocationId
        );
        _context.Districts.Add(deathDistrict);
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
        Assert.Equal(city.SanctuaryLocationId, result!.SanctuaryLocationId);
        Assert.Equal(city.CityName, result.CityName);
    }

    [Fact]
    public async Task Handle_PicksTheCheaperTemple_WhenMultipleAreReachable()
    {
        // Arrange
        var nearCity = await SeedCityWithTemple("Nearhaven");
        var farCity = await SeedCityWithTemple("Farhaven");
        var wilderness = Builders.MakeLocation(WorldId, stateId: nearCity.StateId);
        _context.Locations.Add(wilderness);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedTravelConnector(wilderness.Id, nearCity.CityEntranceLocationId, distance: 1);
        await SeedTravelConnector(wilderness.Id, farCity.CityEntranceLocationId, distance: 5);

        var query = new GetNearestTempleSanctuaryFromLocationQuery
        {
            WorldId = WorldId,
            FromLocationId = wilderness.Id,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nearCity.SanctuaryLocationId, result!.SanctuaryLocationId);
    }

    [Fact]
    public async Task Handle_ResolvesTheStartNode_WhenTheDeathLocationHasNoCity()
    {
        // Arrange
        var city = await SeedCityWithTemple("Dawnreach");
        var wilderness = Builders.MakeLocation(WorldId, stateId: city.StateId);
        var dungeonRoom = Builders.MakeLocation(WorldId, stateId: city.StateId);
        _context.Locations.AddRange(wilderness, dungeonRoom);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedTravelConnector(wilderness.Id, city.CityEntranceLocationId, distance: 1);
        _context.LocationConnectors.Add(
            Builders.MakeLocationConnector(dungeonRoom.Id, wilderness.Id, WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetNearestTempleSanctuaryFromLocationQuery
        {
            WorldId = WorldId,
            FromLocationId = dungeonRoom.Id,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(city.SanctuaryLocationId, result!.SanctuaryLocationId);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoTempleIsReachable()
    {
        // Arrange
        await SeedCityWithTemple("Farflung");
        var isolatedLocation = Builders.MakeLocation(WorldId);
        _context.Locations.Add(isolatedLocation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetNearestTempleSanctuaryFromLocationQuery
        {
            WorldId = WorldId,
            FromLocationId = isolatedLocation.Id,
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    private sealed record CityWithTemple(
        Guid CityId,
        Guid StateId,
        Guid CityEntranceLocationId,
        Guid SanctuaryLocationId,
        string CityName
    );

    private async Task<CityWithTemple> SeedCityWithTemple(string cityName)
    {
        var countryId = Guid.NewGuid();
        var state = Builders.MakeState(countryId, WorldId);
        var city = Builders.MakeCity(state.Id, countryId, worldId: WorldId, name: cityName);
        var cityEntranceDistrict = Builders.MakeDistrict(
            city.Id,
            DistrictType.CityEntrance,
            WorldId
        );
        var cityEntranceLocation = Builders.MakeLocation(
            WorldId,
            stateId: state.Id,
            cityId: city.Id,
            districtId: cityEntranceDistrict.Id,
            id: cityEntranceDistrict.LocationId
        );

        var holySiteDistrict = Builders.MakeDistrict(city.Id, DistrictType.HolySite, WorldId);
        var templeExteriorLocation = Builders.MakeLocation(
            WorldId,
            stateId: state.Id,
            cityId: city.Id,
            districtId: holySiteDistrict.Id,
            id: holySiteDistrict.LocationId
        );
        var temple = Builders.MakeBuilding(
            exteriorLocationId: templeExteriorLocation.Id,
            worldId: WorldId,
            buildingType: BuildingType.Temple
        );
        var sanctuaryLocation = Builders.MakeLocation(WorldId, stateId: state.Id);
        var sanctuaryRoom = Builders.MakeRoom(
            temple.Id,
            worldId: WorldId,
            locationId: sanctuaryLocation.Id,
            name: TempleRoomNames.Sanctuary
        );

        _context.States.Add(state);
        _context.Cities.Add(city);
        _context.Districts.AddRange(cityEntranceDistrict, holySiteDistrict);
        _context.Locations.AddRange(
            cityEntranceLocation,
            templeExteriorLocation,
            sanctuaryLocation
        );
        _context.Buildings.Add(temple);
        _context.Rooms.Add(sanctuaryRoom);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new CityWithTemple(
            city.Id,
            state.Id,
            cityEntranceLocation.Id,
            sanctuaryLocation.Id,
            city.Name
        );
    }

    private async Task SeedTravelConnector(
        Guid originLocationId,
        Guid destinationLocationId,
        float distance
    )
    {
        var outbound = Builders.MakeLocationConnector(
            originLocationId,
            destinationLocationId,
            WorldId
        );
        var inbound = Builders.MakeLocationConnector(
            destinationLocationId,
            originLocationId,
            WorldId
        );
        _context.LocationConnectors.AddRange(outbound, inbound);
        _context.TravelConnectors.AddRange(
            Builders.MakeTravelConnector(outbound.Id, distance, worldId: WorldId),
            Builders.MakeTravelConnector(inbound.Id, distance, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
