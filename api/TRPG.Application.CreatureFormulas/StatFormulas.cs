using TRPG.Application.Configuration;
using TRPG.Data.Models;

namespace TRPG.Application.CreatureFormulas;

public class ActiveBuff
{
    public string AbilityName { get; init; } = "";
    public float Amount { get; init; }
    public AttributeName Attribute { get; init; }
    public int RemainingTurns { get; set; }
    public AmountType AmountType { get; init; }
}

public static class StatFormulas
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
        var extraFlat = attribute switch
        {
            AttributeName.Defense => inventory.OfType<Armor>().Sum(armor => armor.Defense)
                + inventory.OfType<Shield>().Sum(shield => shield.Defense),
            _ => 0,
        };

        var flat =
            buffs
                .Where(buff => buff.Attribute == attribute && buff.AmountType == AmountType.Flat)
                .Sum(buff => buff.Amount)
            + itemModifiers
                .Where(modifier =>
                    modifier.Attribute == attribute && modifier.AmountType == AmountType.Flat
                )
                .Sum(modifier => modifier.Amount)
            + extraFlat;
        var percent =
            buffs
                .Where(buff => buff.Attribute == attribute && buff.AmountType == AmountType.Percent)
                .Sum(buff => buff.Amount)
            + itemModifiers
                .Where(modifier =>
                    modifier.Attribute == attribute && modifier.AmountType == AmountType.Percent
                )
                .Sum(modifier => modifier.Amount);

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

    public static int CalculateMaximumHp(Attributes attributes, CreatureGeneratorOptions options) =>
        Math.Max(1, attributes.Endurance * options.HpPerEndurance);

    public static int CalculateMaximumAp(Attributes attributes, CreatureGeneratorOptions options) =>
        Math.Max(1, attributes.Stamina * options.ApPerStamina);

    public static int CalculateMaximumMp(Attributes attributes, CreatureGeneratorOptions options) =>
        Math.Max(0, attributes.Mana * options.MpPerMana);

    public static int CalculateUnallocatedAttributePoints(
        Attributes attributes,
        int level,
        CreatureGeneratorOptions options
    )
    {
        var expectedTotal = options.BaseAttributes.Total() + level * options.PointsPerLevel;
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

    public static void Recalculate(Creature creature, IReadOnlyCollection<Item> equippedItems)
    {
        var buffs = ToActiveBuffs(creature);
        var effective = CalculateEffectiveAttributes(creature.BaseAttributes, buffs, equippedItems);

        creature.Strength = effective.Strength;
        creature.Dexterity = effective.Dexterity;
        creature.Intelligence = effective.Intelligence;
        creature.Endurance = effective.Endurance;
        creature.Stamina = effective.Stamina;
        creature.Mana = effective.Mana;
        creature.Defense = effective.Defense;
        creature.MaximumHp = effective.MaximumHp;
        creature.MaximumAp = effective.MaximumAp;
        creature.MaximumMp = effective.MaximumMp;
        creature.MovementSpeed = effective.MovementSpeed;
        creature.PhysicalResistance = effective.PhysicalResistance;
        creature.FireResistance = effective.FireResistance;
        creature.IceResistance = effective.IceResistance;
        creature.LightningResistance = effective.LightningResistance;
        creature.PoisonResistance = effective.PoisonResistance;
        creature.MagicResistance = effective.MagicResistance;

        creature.CurrentHp = Math.Min(creature.CurrentHp, creature.MaximumHp);
        creature.CurrentAp = Math.Min(creature.CurrentAp, creature.MaximumAp);
        creature.CurrentMp = Math.Min(creature.CurrentMp, creature.MaximumMp);
    }

    public static IReadOnlyCollection<ActiveBuff> ToActiveBuffs(Creature creature) =>
        creature
            .ActiveBuffs.Select(buff => new ActiveBuff
            {
                Amount = buff.Amount,
                Attribute = Enum.Parse<AttributeName>(buff.Attribute),
                RemainingTurns = buff.RemainingTurns,
                AmountType = Enum.Parse<AmountType>(buff.AmountType),
            })
            .ToArray();
}
