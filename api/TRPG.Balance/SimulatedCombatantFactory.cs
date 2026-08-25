using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Domain.Models;

namespace TRPG.Balance;

internal static class SimulatedCombatantFactory
{
    public static Combatant Build(
        CombatantSpec spec,
        bool isPlayer,
        CreatureGeneratorOptions creatureGeneratorOptions,
        CombatOptions combatOptions
    )
    {
        var attributes = spec.Attributes with
        {
            MaximumHp = StatFormulas.CalculateMaximumHp(spec.Attributes, creatureGeneratorOptions),
            MaximumAp = StatFormulas.CalculateMaximumAp(spec.Attributes, creatureGeneratorOptions),
            MaximumMp = StatFormulas.CalculateMaximumMp(spec.Attributes, creatureGeneratorOptions),
            CarryingCapacity = StatFormulas.CalculateCarryingCapacity(
                spec.Attributes,
                creatureGeneratorOptions
            ),
        };

        var creature = new Creature
        {
            WorldId = Guid.NewGuid(),
            Name = spec.Name,
            CreatureType = CreatureType.Human,
            BirthLocationId = Guid.NewGuid(),
            BirthYear = 1000,
            Level = 1,
            State = CreatureState.Idle,
            BaseAttributes = attributes,
            CurrentHp = attributes.MaximumHp,
            CurrentAp = attributes.MaximumAp,
            CurrentMp = attributes.MaximumMp,
        };

        var abilities = spec
            .SkillLevels.SelectMany(sl =>
                AbilityCatalog.Abilities.Where(a =>
                    a.Skill == sl.Key && a.RequiredSkillLevel <= sl.Value
                )
            )
            .Append(AbilityCatalog.Strike)
            .Distinct()
            .ToArray();

        var items = BuildBaselineGear(spec.SkillLevels, creature.WorldId);

        return Combatant.FromCreature(
            combatOptions,
            isPlayer,
            creature,
            abilities,
            items,
            weaponProficiencies: new Dictionary<WeaponType, int>()
        );
    }

    private static List<Item> BuildBaselineGear(
        IReadOnlyDictionary<Skill, int> skillLevels,
        Guid worldId
    )
    {
        var items = new List<Item>();

        if (skillLevels.GetValueOrDefault(Skill.Melee) > 0)
        {
            items.Add(MakeWeapon(worldId, WeaponType.Sword));
        }
        else if (skillLevels.GetValueOrDefault(Skill.Archery) > 0)
        {
            items.Add(MakeWeapon(worldId, WeaponType.Bow));
        }

        if (skillLevels.GetValueOrDefault(Skill.Blocking) > 0)
        {
            items.Add(MakeShield(worldId));
        }

        return items;
    }

    private static Weapon MakeWeapon(Guid worldId, WeaponType type) =>
        new()
        {
            WorldId = worldId,
            Name = $"Baseline {type}",
            Description = "A plain, fixed-stat baseline weapon for balance simulation.",
            Weight = 8,
            GoldValue = 50,
            Type = type,
            MinDamage = 5,
            MaxDamage = 15,
            Range = 1,
            AttacksPerTurn = 1,
            DurabilityMax = 100,
            DurabilityCurrent = 100,
            Ownership = new ItemOwnership { EquippedSlot = EquipmentSlot.RightHand },
        };

    private static Shield MakeShield(Guid worldId) =>
        new()
        {
            WorldId = worldId,
            Name = "Baseline Shield",
            Description = "A plain, fixed-stat baseline shield for balance simulation.",
            Weight = 8,
            GoldValue = 30,
            Defense = 8,
            BlockChance = 0.25f,
            Modifiers =
            [
                MakeResistanceModifier(AttributeName.MagicResistance),
                MakeResistanceModifier(AttributeName.FireResistance),
                MakeResistanceModifier(AttributeName.IceResistance),
                MakeResistanceModifier(AttributeName.LightningResistance),
                MakeResistanceModifier(AttributeName.PoisonResistance),
            ],
            DurabilityMax = 100,
            DurabilityCurrent = 100,
            Ownership = new ItemOwnership { EquippedSlot = EquipmentSlot.LeftHand },
        };

    private static AttributeModifier MakeResistanceModifier(AttributeName attribute) =>
        new()
        {
            Attribute = attribute,
            AmountType = AmountType.Flat,
            Amount = 0.07f,
        };
}
