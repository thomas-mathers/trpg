using ContractAttributeName = TRPG.Contracts.Combat.Responses.AttributeName;
using DataAttributeName = TRPG.Data.Models.AttributeName;

namespace TRPG.Creatures.Mappers;

internal static class AttributeNameMapper
{
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
}
