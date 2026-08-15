using AbilitiesConditionType = TRPG.Application.Abilities.ConditionType;
using ContractAbilitySkill = TRPG.Contracts.Abilities.Responses.Skill;
using ContractAmountType = TRPG.Contracts.Combat.Responses.AmountType;
using ContractArmorClass = TRPG.Contracts.Inventory.Responses.ArmorClass;
using ContractAttributeName = TRPG.Contracts.Combat.Responses.AttributeName;
using ContractBuildingType = TRPG.Contracts.Scenes.Responses.BuildingType;
using ContractCombatOutcome = TRPG.Contracts.Combat.Responses.CombatOutcome;
using ContractCombatSpeedType = TRPG.Contracts.Inventory.Responses.CombatSpeedType;
using ContractConditionType = TRPG.Contracts.Combat.Responses.ConditionType;
using ContractCreatureState = TRPG.Contracts.Scenes.Responses.CreatureState;
using ContractCreatureType = TRPG.Contracts.Scenes.Responses.CreatureType;
using ContractDamageType = TRPG.Contracts.Combat.Responses.DamageType;
using ContractDistrictType = TRPG.Contracts.Scenes.Responses.DistrictType;
using ContractEquipmentSlot = TRPG.Contracts.Inventory.Responses.EquipmentSlot;
using ContractGender = TRPG.Contracts.Worlds.Requests.Gender;
using ContractItemRarity = TRPG.Contracts.Inventory.Responses.ItemRarity;
using ContractLeechType = TRPG.Contracts.Inventory.Responses.LeechType;
using ContractProcTrigger = TRPG.Contracts.Inventory.Responses.ProcTrigger;
using ContractProfession = TRPG.Contracts.Scenes.Responses.Profession;
using ContractResourceType = TRPG.Contracts.Inventory.Responses.ResourceType;
using ContractSpecialHitType = TRPG.Contracts.Inventory.Responses.SpecialHitType;
using DataAmountType = TRPG.Data.Models.AmountType;
using DataArmorClass = TRPG.Data.Models.ArmorClass;
using DataAttributeName = TRPG.Data.Models.AttributeName;
using DataBuildingType = TRPG.Data.Models.BuildingType;
using DataCombatOutcome = TRPG.Data.Models.CombatOutcome;
using DataCombatSpeedType = TRPG.Data.Models.CombatSpeedType;
using DataCreatureState = TRPG.Data.Models.CreatureState;
using DataCreatureType = TRPG.Data.Models.CreatureType;
using DataDamageType = TRPG.Data.Models.DamageType;
using DataDistrictType = TRPG.Data.Models.DistrictType;
using DataEquipmentSlot = TRPG.Data.Models.EquipmentSlot;
using DataGender = TRPG.Data.Models.Gender;
using DataItemRarity = TRPG.Data.Models.ItemRarity;
using DataLeechType = TRPG.Data.Models.LeechType;
using DataProcTrigger = TRPG.Data.Models.ProcTrigger;
using DataProfession = TRPG.Data.Models.Profession;
using DataResourceType = TRPG.Data.Models.ResourceType;
using DataSkill = TRPG.Data.Models.Skill;
using DataSpecialHitType = TRPG.Data.Models.SpecialHitType;

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
            DataSkill.Melee => ContractAbilitySkill.Melee,
            DataSkill.Unarmed => ContractAbilitySkill.Unarmed,
            DataSkill.Sneak => ContractAbilitySkill.Sneak,
            DataSkill.Destruction => ContractAbilitySkill.Destruction,
            DataSkill.Illusion => ContractAbilitySkill.Illusion,
            DataSkill.Archery => ContractAbilitySkill.Archery,
            DataSkill.Restoration => ContractAbilitySkill.Restoration,
            DataSkill.Alteration => ContractAbilitySkill.Alteration,
            DataSkill.General => ContractAbilitySkill.General,
            DataSkill.Blocking => ContractAbilitySkill.Blocking,
            _ => throw new ArgumentOutOfRangeException(nameof(skill), skill, null),
        };

    public static ContractConditionType ToContract(this AbilitiesConditionType condition) =>
        condition switch
        {
            AbilitiesConditionType.Blinded => ContractConditionType.Blinded,
            AbilitiesConditionType.Bleeding => ContractConditionType.Bleeding,
            AbilitiesConditionType.Burning => ContractConditionType.Burning,
            AbilitiesConditionType.Disarmed => ContractConditionType.Disarmed,
            AbilitiesConditionType.Frozen => ContractConditionType.Frozen,
            AbilitiesConditionType.Poisoned => ContractConditionType.Poisoned,
            AbilitiesConditionType.Silenced => ContractConditionType.Silenced,
            AbilitiesConditionType.Snared => ContractConditionType.Snared,
            AbilitiesConditionType.Stunned => ContractConditionType.Stunned,
            _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, null),
        };

    public static ContractCombatOutcome ToContract(this DataCombatOutcome outcome) =>
        outcome switch
        {
            DataCombatOutcome.Ongoing => ContractCombatOutcome.Ongoing,
            DataCombatOutcome.Victory => ContractCombatOutcome.Victory,
            DataCombatOutcome.Defeat => ContractCombatOutcome.Defeat,
            DataCombatOutcome.Fled => ContractCombatOutcome.Fled,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };

    public static ContractItemRarity ToContract(this DataItemRarity rarity) =>
        rarity switch
        {
            DataItemRarity.Low => ContractItemRarity.Low,
            DataItemRarity.Normal => ContractItemRarity.Normal,
            DataItemRarity.Magic => ContractItemRarity.Magic,
            DataItemRarity.Rare => ContractItemRarity.Rare,
            DataItemRarity.Unique => ContractItemRarity.Unique,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null),
        };

    public static ContractEquipmentSlot ToContract(this DataEquipmentSlot slot) =>
        slot switch
        {
            DataEquipmentSlot.Helm => ContractEquipmentSlot.Helm,
            DataEquipmentSlot.Chest => ContractEquipmentSlot.Chest,
            DataEquipmentSlot.LeftHand => ContractEquipmentSlot.LeftHand,
            DataEquipmentSlot.RightHand => ContractEquipmentSlot.RightHand,
            DataEquipmentSlot.Boots => ContractEquipmentSlot.Boots,
            DataEquipmentSlot.Necklace => ContractEquipmentSlot.Necklace,
            DataEquipmentSlot.Gloves => ContractEquipmentSlot.Gloves,
            DataEquipmentSlot.LeftRing => ContractEquipmentSlot.LeftRing,
            DataEquipmentSlot.RightRing => ContractEquipmentSlot.RightRing,
            DataEquipmentSlot.Belt => ContractEquipmentSlot.Belt,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
        };

    public static ContractArmorClass ToContract(this DataArmorClass armorClass) =>
        armorClass switch
        {
            DataArmorClass.Cloth => ContractArmorClass.Cloth,
            DataArmorClass.Leather => ContractArmorClass.Leather,
            DataArmorClass.Mail => ContractArmorClass.Mail,
            DataArmorClass.Plate => ContractArmorClass.Plate,
            _ => throw new ArgumentOutOfRangeException(nameof(armorClass), armorClass, null),
        };

    public static ContractCombatSpeedType ToContract(this DataCombatSpeedType type) =>
        type switch
        {
            DataCombatSpeedType.IncreasedAttackSpeed =>
                ContractCombatSpeedType.IncreasedAttackSpeed,
            DataCombatSpeedType.FasterCastRate => ContractCombatSpeedType.FasterCastRate,
            DataCombatSpeedType.FasterHitRecovery => ContractCombatSpeedType.FasterHitRecovery,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

    public static ContractLeechType ToContract(this DataLeechType type) =>
        type switch
        {
            DataLeechType.Life => ContractLeechType.Life,
            DataLeechType.Mana => ContractLeechType.Mana,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

    public static ContractSpecialHitType ToContract(this DataSpecialHitType type) =>
        type switch
        {
            DataSpecialHitType.CrushingBlow => ContractSpecialHitType.CrushingBlow,
            DataSpecialHitType.DeadlyStrike => ContractSpecialHitType.DeadlyStrike,
            DataSpecialHitType.OpenWounds => ContractSpecialHitType.OpenWounds,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

    public static ContractProcTrigger ToContract(this DataProcTrigger trigger) =>
        trigger switch
        {
            DataProcTrigger.OnStriking => ContractProcTrigger.OnStriking,
            DataProcTrigger.WhenStruck => ContractProcTrigger.WhenStruck,
            DataProcTrigger.OnKill => ContractProcTrigger.OnKill,
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null),
        };
}
