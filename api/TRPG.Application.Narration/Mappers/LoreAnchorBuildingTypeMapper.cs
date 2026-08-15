using TRPG.Contracts;
using ContractBuildingType = TRPG.Contracts.Scenes.Responses.BuildingType;
using ContractDistrictType = TRPG.Contracts.Scenes.Responses.DistrictType;
using DataBuildingType = TRPG.Data.Models.BuildingType;
using DataDistrictType = TRPG.Data.Models.DistrictType;

namespace TRPG.Application.Narration.Mappers;

internal static class LoreAnchorBuildingTypeMapper
{
    public static string ToDisplayName(DataDistrictType type) =>
        (
            type switch
            {
                DataDistrictType.Residential => ContractDistrictType.Residential,
                DataDistrictType.Scientific => ContractDistrictType.Scientific,
                DataDistrictType.CityCenter => ContractDistrictType.CityCenter,
                DataDistrictType.CityEntrance => ContractDistrictType.CityEntrance,
                DataDistrictType.Governmental => ContractDistrictType.Governmental,
                DataDistrictType.HolySite => ContractDistrictType.HolySite,
                DataDistrictType.Encampment => ContractDistrictType.Encampment,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            }
        ).ToDisplayName();

    public static string ToDisplayName(DataBuildingType type) =>
        (
            type switch
            {
                DataBuildingType.ArcaneShop => ContractBuildingType.ArcaneShop,
                DataBuildingType.Apothecary => ContractBuildingType.Apothecary,
                DataBuildingType.Bakery => ContractBuildingType.Bakery,
                DataBuildingType.Barracks => ContractBuildingType.Barracks,
                DataBuildingType.Blacksmith => ContractBuildingType.Blacksmith,
                DataBuildingType.Carpenter => ContractBuildingType.Carpenter,
                DataBuildingType.Castle => ContractBuildingType.Castle,
                DataBuildingType.Cave => ContractBuildingType.Cave,
                DataBuildingType.Crypt => ContractBuildingType.Crypt,
                DataBuildingType.GeneralGoods => ContractBuildingType.GeneralGoods,
                DataBuildingType.GuildHall => ContractBuildingType.GuildHall,
                DataBuildingType.House => ContractBuildingType.House,
                DataBuildingType.Inn => ContractBuildingType.Inn,
                DataBuildingType.Jail => ContractBuildingType.Jail,
                DataBuildingType.Jeweler => ContractBuildingType.Jeweler,
                DataBuildingType.Library => ContractBuildingType.Library,
                DataBuildingType.Mine => ContractBuildingType.Mine,
                DataBuildingType.Ruins => ContractBuildingType.Ruins,
                DataBuildingType.Stable => ContractBuildingType.Stable,
                DataBuildingType.Tailor => ContractBuildingType.Tailor,
                DataBuildingType.Tavern => ContractBuildingType.Tavern,
                DataBuildingType.Temple => ContractBuildingType.Temple,
                DataBuildingType.Tower => ContractBuildingType.Tower,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            }
        ).ToDisplayName();
}
