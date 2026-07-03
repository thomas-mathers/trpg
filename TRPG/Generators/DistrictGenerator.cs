using TRPG.Models;

namespace TRPG.Generators;

internal static class DistrictGenerator {
    internal static readonly Dictionary<BuildingType, DistrictType> DistrictTypeByBuildingType = new() {
        [BuildingType.Castle] = DistrictType.Castle,
        [BuildingType.GuildHall] = DistrictType.Civic,
        [BuildingType.Jail] = DistrictType.Civic,
        [BuildingType.Library] = DistrictType.Civic,
        [BuildingType.GeneralGoods] = DistrictType.Market,
        [BuildingType.Bakery] = DistrictType.Market,
        [BuildingType.Stable] = DistrictType.Market,
        [BuildingType.Tavern] = DistrictType.Market,
        [BuildingType.ArcaneShop] = DistrictType.Craftsman,
        [BuildingType.Apothecary] = DistrictType.Craftsman,
        [BuildingType.Blacksmith] = DistrictType.Craftsman,
        [BuildingType.Temple] = DistrictType.Craftsman
    };

    private static readonly Dictionary<DistrictType, string[]> Names = new() {
        [DistrictType.Residential] = [
            "The Residential District", "The Old Town", "Hearthside", "The Homesteads", "The Cottage Row"
        ],
        [DistrictType.Castle] = [
            "Castle Square", "The Royal Quarter", "The Keep District", "The Crown Ward", "The Citadel Grounds"
        ],
        [DistrictType.Civic] = [
            "The City Center", "The Civic Quarter", "The Hall District", "The Chancellery Ward", "The Records Quarter"
        ],
        [DistrictType.Market] = [
            "Market Row", "The Grand Bazaar", "Trader's Commons", "The Merchant Quarter", "The Exchange District"
        ],
        [DistrictType.Craftsman] = [
            "The Craftsman's Quarter", "The Old Quarter", "Artisan's Row", "The Forge District", "The Trade Ward"
        ]
    };

    public static District Generate(DistrictType type, Guid cityId) {
        var names = Names[type];
        return new District {
            CityId = cityId,
            DistrictType = type,
            Name = names[Random.Shared.Next(names.Length)]
        };
    }
}
