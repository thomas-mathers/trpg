using ContractBuildingType = TRPG.Contracts.Scenes.Responses.BuildingType;
using ContractCreatureState = TRPG.Contracts.Scenes.Responses.CreatureState;
using ContractCreatureType = TRPG.Contracts.Scenes.Responses.CreatureType;
using ContractDistrictType = TRPG.Contracts.Scenes.Responses.DistrictType;
using ContractGender = TRPG.Contracts.Worlds.Requests.Gender;
using ContractProfession = TRPG.Contracts.Scenes.Responses.Profession;
using DataBuildingType = TRPG.Data.Models.BuildingType;
using DataCreatureState = TRPG.Data.Models.CreatureState;
using DataCreatureType = TRPG.Data.Models.CreatureType;
using DataDistrictType = TRPG.Data.Models.DistrictType;
using DataGender = TRPG.Data.Models.Gender;
using DataProfession = TRPG.Data.Models.Profession;

namespace TRPG.Application.Scenes.Mappers;

internal static class SceneResponseEnumMappers
{
    public static ContractGender ToContract(this DataGender gender) =>
        gender switch
        {
            DataGender.Male => ContractGender.Male,
            DataGender.Female => ContractGender.Female,
            _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
        };

    public static ContractCreatureType ToContract(this DataCreatureType type) =>
        type switch
        {
            DataCreatureType.Human => ContractCreatureType.Human,
            DataCreatureType.Elf => ContractCreatureType.Elf,
            DataCreatureType.Dwarf => ContractCreatureType.Dwarf,
            DataCreatureType.Orc => ContractCreatureType.Orc,
            DataCreatureType.Halfling => ContractCreatureType.Halfling,
            DataCreatureType.Gnome => ContractCreatureType.Gnome,
            DataCreatureType.Undead => ContractCreatureType.Undead,
            DataCreatureType.Demon => ContractCreatureType.Demon,
            DataCreatureType.Beast => ContractCreatureType.Beast,
            DataCreatureType.Construct => ContractCreatureType.Construct,
            DataCreatureType.Elemental => ContractCreatureType.Elemental,
            DataCreatureType.Goblin => ContractCreatureType.Goblin,
            DataCreatureType.Wraith => ContractCreatureType.Wraith,
            DataCreatureType.Giant => ContractCreatureType.Giant,
            DataCreatureType.Dragon => ContractCreatureType.Dragon,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

    public static ContractProfession ToContract(this DataProfession profession) =>
        profession switch
        {
            DataProfession.Knight => ContractProfession.Knight,
            DataProfession.Rogue => ContractProfession.Rogue,
            DataProfession.Ranger => ContractProfession.Ranger,
            DataProfession.Mage => ContractProfession.Mage,
            DataProfession.Cleric => ContractProfession.Cleric,
            DataProfession.Mercenary => ContractProfession.Mercenary,
            DataProfession.Alchemist => ContractProfession.Alchemist,
            DataProfession.Blacksmith => ContractProfession.Blacksmith,
            DataProfession.Scholar => ContractProfession.Scholar,
            DataProfession.Merchant => ContractProfession.Merchant,
            DataProfession.Politician => ContractProfession.Politician,
            DataProfession.StableMaster => ContractProfession.StableMaster,
            DataProfession.Guard => ContractProfession.Guard,
            DataProfession.Baker => ContractProfession.Baker,
            DataProfession.Innkeeper => ContractProfession.Innkeeper,
            DataProfession.Tailor => ContractProfession.Tailor,
            DataProfession.Carpenter => ContractProfession.Carpenter,
            DataProfession.Jeweler => ContractProfession.Jeweler,
            DataProfession.Homemaker => ContractProfession.Homemaker,
            DataProfession.Unemployed => ContractProfession.Unemployed,
            _ => throw new ArgumentOutOfRangeException(nameof(profession), profession, null),
        };

    public static ContractCreatureState ToContract(this DataCreatureState state) =>
        state switch
        {
            DataCreatureState.Idle => ContractCreatureState.Idle,
            DataCreatureState.Sleeping => ContractCreatureState.Sleeping,
            DataCreatureState.Busy => ContractCreatureState.Busy,
            DataCreatureState.Studying => ContractCreatureState.Studying,
            DataCreatureState.Praying => ContractCreatureState.Praying,
            DataCreatureState.Training => ContractCreatureState.Training,
            DataCreatureState.Sitting => ContractCreatureState.Sitting,
            DataCreatureState.Alerted => ContractCreatureState.Alerted,
            DataCreatureState.Dead => ContractCreatureState.Dead,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };

    public static ContractDistrictType ToContract(this DataDistrictType type) =>
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
        };

    public static ContractBuildingType ToContract(this DataBuildingType type) =>
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
        };
}
