using TRPG.Application.WorldGeneration;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Tests.Helpers;

public sealed record SeededSanctuary(Guid StateId, Guid SanctuaryLocationId);

public static class SanctuarySeeder
{
    public static async Task<SeededSanctuary> SeedCityWithTemple(
        TrpgDbContext context,
        Guid worldId,
        Guid deathLocationId
    )
    {
        var countryId = Guid.NewGuid();
        var state = Builders.MakeState(countryId, worldId);
        var city = Builders.MakeCity(state.Id, countryId, worldId: worldId);
        var cityEntranceDistrict = Builders.MakeDistrict(
            city.Id,
            DistrictType.CityEntrance,
            worldId,
            locationId: deathLocationId
        );
        var deathLocation = Builders.MakeLocation(
            worldId,
            stateId: state.Id,
            cityId: city.Id,
            districtId: cityEntranceDistrict.Id,
            id: deathLocationId,
            coarseAnchorLocationId: deathLocationId
        );
        var holySiteDistrict = Builders.MakeDistrict(city.Id, DistrictType.HolySite, worldId);
        var templeExteriorLocation = Builders.MakeLocation(
            worldId,
            stateId: state.Id,
            cityId: city.Id,
            districtId: holySiteDistrict.Id,
            id: holySiteDistrict.LocationId
        );
        var temple = Builders.MakeBuilding(
            exteriorLocationId: templeExteriorLocation.Id,
            worldId: worldId,
            buildingType: BuildingType.Temple
        );
        var sanctuaryLocation = Builders.MakeLocation(
            worldId,
            stateId: state.Id,
            coarseAnchorLocationId: deathLocationId
        );
        var sanctuaryRoom = Builders.MakeRoom(
            temple.Id,
            worldId: worldId,
            locationId: sanctuaryLocation.Id,
            name: TempleRoomNames.Sanctuary
        );

        context.States.Add(state);
        context.Cities.Add(city);
        context.Districts.AddRange(cityEntranceDistrict, holySiteDistrict);
        context.Locations.AddRange(deathLocation, templeExteriorLocation, sanctuaryLocation);
        context.Buildings.Add(temple);
        context.Rooms.Add(sanctuaryRoom);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new SeededSanctuary(state.Id, sanctuaryLocation.Id);
    }
}
