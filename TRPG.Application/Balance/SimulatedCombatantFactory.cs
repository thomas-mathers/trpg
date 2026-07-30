using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Data.Models;

namespace TRPG.Application.Balance;

internal static class SimulatedCombatantFactory
{
    public static Combatant Build(CombatantSpec spec, bool isPlayer, StatFormulas statFormulas)
    {
        var attributes = spec.Attributes with
        {
            MaximumHp = statFormulas.CalculateMaximumHp(spec.Attributes),
            MaximumAp = statFormulas.CalculateMaximumAp(spec.Attributes),
            MaximumMp = statFormulas.CalculateMaximumMp(spec.Attributes),
        };

        var creature = new Creature
        {
            WorldId = Guid.NewGuid(),
            Name = spec.Name,
            CreatureType = CreatureType.Human,
            BirthStateId = Guid.NewGuid(),
            BirthYear = 1000,
            StateId = Guid.NewGuid(),
            Level = 1,
            State = CreatureState.Idle,
            BaseAttributes = attributes,
            CurrentHp = attributes.MaximumHp,
            CurrentAp = attributes.MaximumAp,
            CurrentMp = attributes.MaximumMp,
        };

        var abilityDefinitions = AbilityDefinitions.Create();
        var abilities = spec
            .SkillLevels.SelectMany(sl =>
                abilityDefinitions.Abilities.Where(a =>
                    a.Skill == sl.Key && a.RequiredSkillLevel <= sl.Value
                )
            )
            .Append(abilityDefinitions.BasicAttack)
            .Distinct()
            .ToArray();

        var items = BuildBaselineGear(spec.SkillLevels, creature.WorldId);

        return Combatant.FromCreature(
            creature,
            abilities,
            isPlayer,
            items,
            weaponProficiencies: new Dictionary<WeaponType, int>()
        );
    }

    // A fixed-stat baseline weapon/shield, one per gear-gated skill tree the spec actually
    // invests in (see AbilityGearRequirement) - no procedural quality variance, just enough
    // for that tree's abilities to be usable. Melee takes priority over Archery if a spec
    // somehow invests in both, since a real build would typically commit to one.
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
            AttackSpeed = 7,
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
            MagicResistance = 0.07f,
            FireResistance = 0.07f,
            IceResistance = 0.07f,
            LightningResistance = 0.07f,
            PoisonResistance = 0.07f,
            DurabilityMax = 100,
            DurabilityCurrent = 100,
            Ownership = new ItemOwnership { EquippedSlot = EquipmentSlot.LeftHand },
        };
}
