using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures;

public class ActiveBuff
{
    public string AbilityName { get; init; } = "";
    public float Amount { get; init; }
    public AttributeName Attribute { get; init; }
    public int RemainingTurns { get; set; }
    public AmountType AmountType { get; init; }
}

public class StatFormulas(IOptionsSnapshot<CreatureGeneratorOptions> optionsSnapshot)
{
    public static float CalculateEffectiveAttribute(
        Attributes attributes,
        IReadOnlyCollection<ActiveBuff> buffs,
        IReadOnlyCollection<Item> inventory,
        AttributeName attribute
    )
    {
        var baseValue = attribute switch
        {
            AttributeName.Strength => attributes.Strength,
            AttributeName.Dexterity => attributes.Dexterity,
            AttributeName.Intelligence => attributes.Intelligence,
            AttributeName.Endurance => attributes.Endurance,
            AttributeName.Stamina => attributes.Stamina,
            AttributeName.Mana => attributes.Mana,
            AttributeName.Defense => attributes.Defense,
            AttributeName.MaximumHp => attributes.MaximumHp,
            AttributeName.MaximumAp => attributes.MaximumAp,
            AttributeName.MaximumMp => attributes.MaximumMp,
            AttributeName.MovementSpeed => attributes.MovementSpeed,
            AttributeName.PhysicalResistance => attributes.PhysicalResistance,
            AttributeName.FireResistance => attributes.FireResistance,
            AttributeName.IceResistance => attributes.IceResistance,
            AttributeName.LightningResistance => attributes.LightningResistance,
            AttributeName.PoisonResistance => attributes.PoisonResistance,
            AttributeName.MagicResistance => attributes.MagicResistance,
            _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null),
        };

        var itemModifiers = inventory
            .SelectMany(item => item.Modifiers)
            .OfType<AttributeModifier>()
            .ToArray();
        var extraFlat =
            attribute == AttributeName.Defense
                ? inventory.OfType<Armor>().Sum(armor => armor.Defense)
                    + inventory.OfType<Shield>().Sum(shield => shield.Defense)
                : 0;

        var flat =
            buffs
                .Where(b => b.Attribute == attribute && b.AmountType == AmountType.Flat)
                .Sum(b => b.Amount)
            + itemModifiers
                .Where(m => m.Attribute == attribute && m.AmountType == AmountType.Flat)
                .Sum(m => m.Amount)
            + extraFlat;
        var percent =
            buffs
                .Where(b => b.Attribute == attribute && b.AmountType == AmountType.Percent)
                .Sum(b => b.Amount)
            + itemModifiers
                .Where(m => m.Attribute == attribute && m.AmountType == AmountType.Percent)
                .Sum(m => m.Amount);

        return (baseValue + flat) * (1 + percent / 100f);
    }

    public static Attributes CalculateEffectiveAttributes(
        Attributes baseAttributes,
        IReadOnlyCollection<ActiveBuff> buffs,
        IReadOnlyCollection<Item> inventory
    )
    {
        float CalculateEffective(AttributeName attribute) =>
            CalculateEffectiveAttribute(baseAttributes, buffs, inventory, attribute);

        return new Attributes
        {
            Strength = (int)CalculateEffective(AttributeName.Strength),
            Dexterity = (int)CalculateEffective(AttributeName.Dexterity),
            Intelligence = (int)CalculateEffective(AttributeName.Intelligence),
            Endurance = (int)CalculateEffective(AttributeName.Endurance),
            Stamina = (int)CalculateEffective(AttributeName.Stamina),
            Mana = (int)CalculateEffective(AttributeName.Mana),
            Defense = (int)CalculateEffective(AttributeName.Defense),
            MaximumHp = (int)CalculateEffective(AttributeName.MaximumHp),
            MaximumAp = (int)CalculateEffective(AttributeName.MaximumAp),
            MaximumMp = (int)CalculateEffective(AttributeName.MaximumMp),
            MovementSpeed = CalculateEffective(AttributeName.MovementSpeed),
            PhysicalResistance = CalculateEffective(AttributeName.PhysicalResistance),
            FireResistance = CalculateEffective(AttributeName.FireResistance),
            IceResistance = CalculateEffective(AttributeName.IceResistance),
            LightningResistance = CalculateEffective(AttributeName.LightningResistance),
            PoisonResistance = CalculateEffective(AttributeName.PoisonResistance),
            MagicResistance = CalculateEffective(AttributeName.MagicResistance),
        };
    }

    public int CalculateMaximumHp(Attributes attributes) =>
        Math.Max(1, attributes.Endurance * optionsSnapshot.Value.HpPerEndurance);

    public int CalculateMaximumAp(Attributes attributes) =>
        Math.Max(1, attributes.Stamina * optionsSnapshot.Value.ApPerStamina);

    public int CalculateMaximumMp(Attributes attributes) =>
        Math.Max(0, attributes.Mana * optionsSnapshot.Value.MpPerMana);

    public int CalculateUnallocatedAttributePoints(Attributes attributes, int level)
    {
        var expectedTotal =
            optionsSnapshot.Value.BaseAttributes.Total()
            + level * optionsSnapshot.Value.PointsPerLevel;
        var currentTotal =
            attributes.Strength
            + attributes.Defense
            + attributes.Dexterity
            + attributes.Endurance
            + attributes.Stamina
            + attributes.Mana
            + attributes.Intelligence;

        return expectedTotal - currentTotal;
    }
}
