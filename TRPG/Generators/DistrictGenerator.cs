using TRPG.Models;

namespace TRPG.Generators;

internal static class DistrictGenerator {
    internal static readonly Dictionary<BuildingType, DistrictType> DistrictTypeByBuildingType = new() {
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
        [BuildingType.Barracks] = DistrictType.Encampment
    };

    internal static readonly Dictionary<DistrictType, int> Popularity = new() {
        [DistrictType.Residential] = 6,
        [DistrictType.Scientific] = 3,
        [DistrictType.CityCenter] = 10,
        [DistrictType.Governmental] = 2,
        [DistrictType.HolySite] = 4,
        [DistrictType.Encampment] = 3
    };

    private static readonly Dictionary<DistrictType, string[]> Names = new() {
        [DistrictType.Residential] = [
            "The Residential District", "The Old Town", "Hearthside", "The Homesteads", "The Cottage Row"
        ],
        [DistrictType.Scientific] = [
            "The Scholar's Quarter", "The Athenaeum District", "The Study Ward", "The Archive Grounds",
            "The Enlightenment Row"
        ],
        [DistrictType.CityCenter] = [
            "The City Center", "The Exchange District", "Trader's Commons", "The Merchant Quarter", "The Grand Bazaar"
        ],
        [DistrictType.Governmental] = [
            "The Chancellery Ward", "Castle Square", "The Seat of Power", "The Administrative Quarter", "The Crown Ward"
        ],
        [DistrictType.HolySite] = [
            "The Sacred Grounds", "The Temple District", "The Hallowed Quarter", "The Pilgrim's Row", "The Shrine Ward"
        ],
        [DistrictType.Encampment] = [
            "The Garrison Grounds", "The Camp District", "The Barracks Row", "The Forge Ward", "The Muster Field"
        ]
    };

    public static District Generate(DistrictType type, Guid cityId, Guid worldId) {
        var names = Names[type];
        return new District {
            CityId = cityId,
            DistrictType = type,
            Name = names[Random.Shared.Next(names.Length)],
            WorldId = worldId
        };
    }
}