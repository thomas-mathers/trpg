using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;

namespace TRPG.Balance;

internal sealed class DiagnosticOptionsSnapshot<T>(T value) : IOptionsSnapshot<T>
    where T : class
{
    public T Value => value;

    public T Get(string? name) => value;
}

internal static class DiagnosticExperiment
{
    public static void Run(Profession profession, CreatureType monsterCreatureType, int level)
    {
        var context = GeneratedCombatantFactory.CreateContext();
        var damageCalculator = new DamageCalculator(
            new DiagnosticOptionsSnapshot<CombatOptions>(new CombatOptions())
        );

        var monsterArchetype = MonsterArchetypeFor(monsterCreatureType);
        var playerArchetype = CreatureArchetype.For(profession);

        var playerInput = new CreatureGeneratorInput(
            CreatureType.Human,
            playerArchetype,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            level,
            level,
            StartingAttributeAllocation: ProfessionMatrixExperiment.BuildRealisticAllocation(
                playerArchetype,
                level
            )
        );
        var playerResult = context.Generator.Generate(playerInput);
        var player = GeneratedCombatantFactory.ToCombatant(playerResult, isPlayer: true);

        var monsterInput = new CreatureGeneratorInput(
            monsterCreatureType,
            monsterArchetype,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            level,
            level
        );
        var monsterResult = context.Generator.Generate(monsterInput);
        var monster = GeneratedCombatantFactory.ToCombatant(monsterResult, isPlayer: false);

        PrintCombatant($"{profession} (level {level})", player);
        PrintCombatant($"{monsterCreatureType} (level {level})", monster);

        Console.WriteLine();
        Console.WriteLine($"--- {profession} abilities vs {monsterCreatureType} ---");
        PrintAbilityDamage(player, monster, damageCalculator);

        Console.WriteLine();
        Console.WriteLine($"--- {monsterCreatureType} abilities vs {profession} ---");
        PrintAbilityDamage(monster, player, damageCalculator);
    }

    private static void PrintCombatant(string label, Combatant combatant)
    {
        Console.WriteLine($"{label}:");
        Console.WriteLine(
            $"  Str={combatant.Strength:F1} "
                + $"Int={combatant.Intelligence:F1} "
                + $"Dex={combatant.Dexterity:F1} "
                + $"Def={combatant.Defense:F1}"
        );
        Console.WriteLine(
            $"  PhysRes={combatant.PhysicalResistance:F2} "
                + $"MagicRes={combatant.MagicResistance:F2} "
                + $"FireRes={combatant.FireResistance:F2} "
                + $"IceRes={combatant.IceResistance:F2} "
                + $"LightningRes={combatant.LightningResistance:F2} "
                + $"PoisonRes={combatant.PoisonResistance:F2}"
        );
        Console.WriteLine(
            $"  MaxHp={combatant.MaximumHp} MaxAp={combatant.MaximumAp} MaxMp={combatant.MaximumMp} "
                + $"Stamina={combatant.Stamina:F1} "
                + $"Mana={combatant.Mana:F1}"
        );
        Console.WriteLine(
            combatant.Weapon is { } weapon
                ? $"  Weapon={weapon.Type} {weapon.MinDamage}-{weapon.MaxDamage} (proficiency={combatant.AttackRating:F1})"
                : "  Weapon=none"
        );
    }

    private static void PrintAbilityDamage(
        Combatant attacker,
        Combatant defender,
        DamageCalculator damageCalculator
    )
    {
        foreach (var ability in attacker.Abilities.OfType<AttackAbility>())
        {
            var damage = damageCalculator.EstimateDamage(attacker, ability, defender);
            Console.WriteLine(
                $"  {ability.Name, -20} {ability.DamageType, -10} amount={ability.DamageAmount, -6} "
                    + $"reqLevel={ability.RequiredSkillLevel, -4} estDamage={damage}"
            );
        }
    }

    internal static CreatureArchetype MonsterArchetypeFor(CreatureType creatureType) =>
        creatureType switch
        {
            CreatureType.Beast => CreatureArchetype.Beast,
            CreatureType.Undead => CreatureArchetype.Undead,
            CreatureType.Construct => CreatureArchetype.Construct,
            CreatureType.Demon => CreatureArchetype.Demon,
            CreatureType.Elemental => CreatureArchetype.Elemental,
            CreatureType.Goblin => CreatureArchetype.Goblin,
            CreatureType.Wraith => CreatureArchetype.Wraith,
            CreatureType.Giant => CreatureArchetype.Giant,
            CreatureType.Dragon => CreatureArchetype.Dragon,
            _ => throw new ArgumentOutOfRangeException(nameof(creatureType)),
        };
}
