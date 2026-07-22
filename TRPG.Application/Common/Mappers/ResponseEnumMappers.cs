using AbilitiesConditionType = TRPG.Application.Abilities.ConditionType;
using ContractAbilitySkill = TRPG.Contracts.Abilities.Responses.Skill;
using ContractAmountType = TRPG.Contracts.Combat.Responses.AmountType;
using ContractAttributeName = TRPG.Contracts.Combat.Responses.AttributeName;
using ContractBuildingType = TRPG.Contracts.Scenes.Responses.BuildingType;
using ContractConditionType = TRPG.Contracts.Combat.Responses.ConditionType;
using ContractCreatureState = TRPG.Contracts.Scenes.Responses.CreatureState;
using ContractCreatureType = TRPG.Contracts.Scenes.Responses.CreatureType;
using ContractDamageType = TRPG.Contracts.Combat.Responses.DamageType;
using ContractDistrictType = TRPG.Contracts.Scenes.Responses.DistrictType;
using ContractGender = TRPG.Contracts.Worlds.Requests.Gender;
using ContractProfession = TRPG.Contracts.Scenes.Responses.Profession;
using ContractResourceType = TRPG.Contracts.Inventory.Responses.ResourceType;
using DataAmountType = TRPG.Data.Models.AmountType;
using DataAttributeName = TRPG.Data.Models.AttributeName;
using DataBuildingType = TRPG.Data.Models.BuildingType;
using DataCreatureState = TRPG.Data.Models.CreatureState;
using DataCreatureType = TRPG.Data.Models.CreatureType;
using DataDamageType = TRPG.Data.Models.DamageType;
using DataDistrictType = TRPG.Data.Models.DistrictType;
using DataGender = TRPG.Data.Models.Gender;
using DataProfession = TRPG.Data.Models.Profession;
using DataResourceType = TRPG.Data.Models.ResourceType;
using DataSkill = TRPG.Data.Models.Skill;

namespace TRPG.Application.Common.Mappers;

internal static class ResponseEnumMappers
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
            DataProfession.Bartender => ContractProfession.Bartender,
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
            DataCreatureState.Sleeping => ContractCreatureState.Sleeping,
            DataCreatureState.Idle => ContractCreatureState.Idle,
            DataCreatureState.Busy => ContractCreatureState.Busy,
            DataCreatureState.Studying => ContractCreatureState.Studying,
            DataCreatureState.Praying => ContractCreatureState.Praying,
            DataCreatureState.Training => ContractCreatureState.Training,
            DataCreatureState.Sitting => ContractCreatureState.Sitting,
            DataCreatureState.Dead => ContractCreatureState.Dead,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };

    public static ContractDistrictType ToContract(this DataDistrictType type) =>
        type switch
        {
            DataDistrictType.Residential => ContractDistrictType.Residential,
            DataDistrictType.Scientific => ContractDistrictType.Scientific,
            DataDistrictType.CityCenter => ContractDistrictType.CityCenter,
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

    public static ContractDamageType ToContract(this DataDamageType type) =>
        type switch
        {
            DataDamageType.Physical => ContractDamageType.Physical,
            DataDamageType.Fire => ContractDamageType.Fire,
            DataDamageType.Ice => ContractDamageType.Ice,
            DataDamageType.Lightning => ContractDamageType.Lightning,
            DataDamageType.Poison => ContractDamageType.Poison,
            DataDamageType.Magic => ContractDamageType.Magic,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

    public static ContractAttributeName ToContract(this DataAttributeName attribute) =>
        attribute switch
        {
            DataAttributeName.MaximumHp => ContractAttributeName.MaximumHp,
            DataAttributeName.MaximumAp => ContractAttributeName.MaximumAp,
            DataAttributeName.MaximumMp => ContractAttributeName.MaximumMp,
            DataAttributeName.Strength => ContractAttributeName.Strength,
            DataAttributeName.Defense => ContractAttributeName.Defense,
            DataAttributeName.Dexterity => ContractAttributeName.Dexterity,
            DataAttributeName.Endurance => ContractAttributeName.Endurance,
            DataAttributeName.Stamina => ContractAttributeName.Stamina,
            DataAttributeName.Mana => ContractAttributeName.Mana,
            DataAttributeName.Intelligence => ContractAttributeName.Intelligence,
            DataAttributeName.PhysicalResistance => ContractAttributeName.PhysicalResistance,
            DataAttributeName.FireResistance => ContractAttributeName.FireResistance,
            DataAttributeName.IceResistance => ContractAttributeName.IceResistance,
            DataAttributeName.LightningResistance => ContractAttributeName.LightningResistance,
            DataAttributeName.PoisonResistance => ContractAttributeName.PoisonResistance,
            DataAttributeName.MagicResistance => ContractAttributeName.MagicResistance,
            DataAttributeName.MovementSpeed => ContractAttributeName.MovementSpeed,
            _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null),
        };

    public static ContractResourceType ToContract(this DataResourceType resource) =>
        resource switch
        {
            DataResourceType.Hp => ContractResourceType.Hp,
            DataResourceType.Ap => ContractResourceType.Ap,
            DataResourceType.Mp => ContractResourceType.Mp,
            _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null),
        };

    public static ContractAmountType ToContract(this DataAmountType type) =>
        type switch
        {
            DataAmountType.Flat => ContractAmountType.Flat,
            DataAmountType.Percent => ContractAmountType.Percent,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

    public static ContractAbilitySkill ToContract(this DataSkill skill) =>
        skill switch
        {
            DataSkill.Swordsmanship => ContractAbilitySkill.Swordsmanship,
            DataSkill.Stealth => ContractAbilitySkill.Stealth,
            DataSkill.Spellcasting => ContractAbilitySkill.Spellcasting,
            DataSkill.Archery => ContractAbilitySkill.Archery,
            DataSkill.Devotion => ContractAbilitySkill.Devotion,
            DataSkill.Warfare => ContractAbilitySkill.Warfare,
            DataSkill.General => ContractAbilitySkill.General,
            _ => throw new ArgumentOutOfRangeException(nameof(skill), skill, null),
        };

    public static ContractConditionType ToContract(this AbilitiesConditionType condition) =>
        condition switch
        {
            AbilitiesConditionType.Blinded => ContractConditionType.Blinded,
            AbilitiesConditionType.Bleeding => ContractConditionType.Bleeding,
            AbilitiesConditionType.Burning => ContractConditionType.Burning,
            AbilitiesConditionType.Frozen => ContractConditionType.Frozen,
            AbilitiesConditionType.Poisoned => ContractConditionType.Poisoned,
            AbilitiesConditionType.Silenced => ContractConditionType.Silenced,
            AbilitiesConditionType.Snared => ContractConditionType.Snared,
            AbilitiesConditionType.Stunned => ContractConditionType.Stunned,
            _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, null),
        };
}
