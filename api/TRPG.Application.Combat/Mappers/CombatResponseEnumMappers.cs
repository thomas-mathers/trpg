using AbilitiesConditionType = TRPG.Application.Abilities.ConditionType;
using ContractAmountType = TRPG.Contracts.Combat.Responses.AmountType;
using ContractAttributeName = TRPG.Contracts.Combat.Responses.AttributeName;
using ContractCombatOutcome = TRPG.Contracts.Combat.Responses.CombatOutcome;
using ContractConditionType = TRPG.Contracts.Combat.Responses.ConditionType;
using ContractDamageType = TRPG.Contracts.Combat.Responses.DamageType;
using ContractResourceType = TRPG.Contracts.Inventory.Responses.ResourceType;
using DataAmountType = TRPG.Data.Models.AmountType;
using DataAttributeName = TRPG.Data.Models.AttributeName;
using DataCombatOutcome = TRPG.Data.Models.CombatOutcome;
using DataDamageType = TRPG.Data.Models.DamageType;
using DataResourceType = TRPG.Data.Models.ResourceType;

namespace TRPG.Application.Combat.Mappers;

internal static class CombatResponseEnumMappers
{
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

    public static ContractAmountType ToContract(this DataAmountType type) =>
        type switch
        {
            DataAmountType.Flat => ContractAmountType.Flat,
            DataAmountType.Percent => ContractAmountType.Percent,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
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

    public static ContractResourceType ToContract(this DataResourceType resource) =>
        resource switch
        {
            DataResourceType.Hp => ContractResourceType.Hp,
            DataResourceType.Ap => ContractResourceType.Ap,
            DataResourceType.Mp => ContractResourceType.Mp,
            _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null),
        };
}
