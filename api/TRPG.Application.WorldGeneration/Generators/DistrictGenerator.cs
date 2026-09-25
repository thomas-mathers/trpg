using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class DistrictGenerator
{
    private static readonly SeatSpec[] PublicSeatSpecs =
    [
        new("Bench", "A sturdy public bench where travelers can rest."),
        new("Stone Bench", "A broad stone bench worn smooth by years of use."),
        new("Low Wall", "A low masonry wall with a flat top suitable for sitting."),
    ];

    internal static readonly Dictionary<BuildingType, DistrictType> DistrictTypeByBuildingType =
        new()
        {
            [BuildingType.Library] = DistrictType.Scientific,
            [BuildingType.ArcaneShop] = DistrictType.Scientific,
            [BuildingType.Apothecary] = DistrictType.Scientific,
            [BuildingType.GeneralGoods] = DistrictType.CityCenter,
            [BuildingType.Bakery] = DistrictType.CityCenter,
            [BuildingType.Tavern] = DistrictType.CityCenter,
            [BuildingType.Inn] = DistrictType.CityCenter,
            [BuildingType.GuildHall] = DistrictType.CityCenter,
            [BuildingType.Tailor] = DistrictType.CityCenter,
            [BuildingType.Carpenter] = DistrictType.CityCenter,
            [BuildingType.Jeweler] = DistrictType.CityCenter,
            [BuildingType.Castle] = DistrictType.Governmental,
            [BuildingType.Jail] = DistrictType.Governmental,
            [BuildingType.Temple] = DistrictType.HolySite,
            [BuildingType.Blacksmith] = DistrictType.Encampment,
            [BuildingType.Stable] = DistrictType.Encampment,
            [BuildingType.Barracks] = DistrictType.Encampment,
        };

    private static readonly Dictionary<DistrictType, string[]> Names = new()
    {
        [DistrictType.Residential] =
        [
            "The Residential District",
            "The Old Town",
            "Hearthside",
            "The Homesteads",
            "The Cottage Row",
        ],
        [DistrictType.Scientific] =
        [
            "The Scholar's Quarter",
            "The Athenaeum District",
            "The Study Ward",
            "The Archive Grounds",
            "The Enlightenment Row",
        ],
        [DistrictType.CityCenter] =
        [
            "The City Center",
            "The Exchange District",
            "Trader's Commons",
            "The Merchant Quarter",
            "The Grand Bazaar",
        ],
        [DistrictType.CityEntrance] =
        [
            "The City Gates",
            "The Gate District",
            "The Outer Gate",
            "The Arrival Square",
            "The Wayfarer's Gate",
        ],
        [DistrictType.Governmental] =
        [
            "The Chancellery Ward",
            "Castle Square",
            "The Seat of Power",
            "The Administrative Quarter",
            "The Crown Ward",
        ],
        [DistrictType.HolySite] =
        [
            "The Sacred Grounds",
            "The Temple District",
            "The Hallowed Quarter",
            "The Pilgrim's Row",
            "The Shrine Ward",
        ],
        [DistrictType.Encampment] =
        [
            "The Garrison Grounds",
            "The Camp District",
            "The Barracks Row",
            "The Forge Ward",
            "The Muster Field",
        ],
    };

    public static DistrictGeneratorResult Generate(
        DistrictType type,
        Guid cityId,
        Guid stateId,
        Guid worldId
    )
    {
        var names = Names[type];
        var districtId = Guid.NewGuid();
        var location = LocationGenerator.Generate(worldId, stateId, cityId, districtId);
        var district = new District
        {
            Id = districtId,
            CityId = cityId,
            DistrictType = type,
            LocationId = location.Id,
            Name = names[Random.Shared.Next(names.Length)],
            WorldId = worldId,
        };
        var seats = PublicSeatSpecs
            .Select(spec => new Seat
            {
                LocationId = location.Id,
                WorldId = worldId,
                Name = spec.Name,
                Description = spec.Description,
            })
            .ToArray();
        return new DistrictGeneratorResult(district, location, seats);
    }

    private sealed record SeatSpec(string Name, string Description);
}

internal record DistrictGeneratorResult(
    District District,
    Location Location,
    IReadOnlyCollection<Seat> Seats
);
