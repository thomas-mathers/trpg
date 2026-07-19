using TRPG.Data.Models;

namespace TRPG.Application.Creatures;

internal static class CreatureAttributesRecalculator
{
    public static void Recalculate(Creature creature, IReadOnlyCollection<Item> equippedItems)
    {
        var buffs = creature
            .ActiveBuffs.Select(b => new ActiveBuff
            {
                Amount = b.Amount,
                Attribute = Enum.Parse<AttributeName>(b.Attribute),
                RemainingTurns = b.RemainingTurns,
                AmountType = Enum.Parse<AmountType>(b.AmountType),
            })
            .ToArray();

        var effective = StatFormulas.CalculateEffectiveAttributes(
            creature.BaseAttributes,
            buffs,
            equippedItems
        );

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
}
