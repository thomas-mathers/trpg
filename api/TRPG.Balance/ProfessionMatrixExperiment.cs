using System.Globalization;
using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Application.Worlds.Generators;
using TRPG.Contracts.Creatures.Requests;
using TRPG.Data.Models;

namespace TRPG.Balance;

internal static class ProfessionMatrixExperiment
{
    private static readonly IReadOnlyList<Profession> PlayableProfessions =
    [
        Profession.Knight,
        Profession.Rogue,
        Profession.Ranger,
        Profession.Mage,
        Profession.Cleric,
        Profession.Mercenary,
    ];

    private static readonly IReadOnlyList<(
        string Label,
        CreatureType CreatureType,
        CreatureArchetype Archetype
    )> MonsterTypes =
    [
        ("Beast", CreatureType.Beast, CreatureArchetype.Beast),
        ("Undead", CreatureType.Undead, CreatureArchetype.Undead),
        ("Construct", CreatureType.Construct, CreatureArchetype.Construct),
        ("Demon", CreatureType.Demon, CreatureArchetype.Demon),
        ("Elemental", CreatureType.Elemental, CreatureArchetype.Elemental),
        ("Goblin", CreatureType.Goblin, CreatureArchetype.Goblin),
        ("Wraith", CreatureType.Wraith, CreatureArchetype.Wraith),
        ("Giant", CreatureType.Giant, CreatureArchetype.Giant),
        ("Dragon", CreatureType.Dragon, CreatureArchetype.Dragon),
    ];

    public static async Task Run(int trialsPerMatchup, int level, string outputPath)
    {
        var context = GeneratedCombatantFactory.CreateContext();
        var simulator = new FightSimulator();

        await using var writer = new StreamWriter(outputPath);
        await writer.WriteLineAsync(
            "profession," + string.Join(',', MonsterTypes.Select(m => m.Label))
        );

        foreach (var profession in PlayableProfessions)
        {
            var winRates = new List<string>();

            foreach (var monsterType in MonsterTypes)
            {
                var wins = 0;

                for (var trial = 1; trial <= trialsPerMatchup; trial++)
                {
                    var player = GeneratePlayer(context, profession, level);
                    var enemy = GenerateMonster(
                        context,
                        monsterType.CreatureType,
                        monsterType.Archetype,
                        level
                    );

                    var snapshots = simulator.RunFight(trial, player, enemy);
                    if (snapshots[^1].Outcome == CombatOutcome.Victory.ToString())
                    {
                        wins++;
                    }
                }

                winRates.Add(
                    ((double)wins / trialsPerMatchup).ToString("P0", CultureInfo.InvariantCulture)
                );
            }

            await writer.WriteLineAsync($"{profession}," + string.Join(',', winRates));
            Console.WriteLine($"{profession}: done");
        }

        Console.WriteLine($"Wrote profession/monster win-rate matrix to {outputPath}");
    }

    internal static Combatant GeneratePlayer(
        GeneratedCombatantContext context,
        Profession profession,
        int level,
        CombatOptions? combatOptions = null
    )
    {
        var archetype = CreatureArchetype.For(profession);
        var input = new CreatureGeneratorInput(
            CreatureType.Human,
            archetype,
            Guid.NewGuid(),
            Guid.NewGuid(),
            level,
            level,
            StartingAttributeAllocation: BuildRealisticAllocation(archetype, level)
        );
        var result = context.Generator.Generate(input);
        return GeneratedCombatantFactory.ToCombatant(result, isPlayer: true, combatOptions);
    }

    internal static IReadOnlyDictionary<AllocatableAttributeName, int> BuildRealisticAllocation(
        CreatureArchetype archetype,
        int level
    )
    {
        const int pointsPerLevel = 5;
        var totalPoints = level * pointsPerLevel;

        var weights = new (AllocatableAttributeName Attribute, int Weight)[]
        {
            (AllocatableAttributeName.Strength, archetype.StatAffinities.Strength),
            (AllocatableAttributeName.Dexterity, archetype.StatAffinities.Dexterity),
            (AllocatableAttributeName.Endurance, archetype.StatAffinities.Endurance),
            (AllocatableAttributeName.Stamina, archetype.StatAffinities.Stamina),
            (AllocatableAttributeName.Mana, archetype.StatAffinities.Mana),
            (AllocatableAttributeName.Intelligence, archetype.StatAffinities.Intelligence),
        };
        var totalWeight = weights.Sum(w => w.Weight);
        if (totalWeight == 0)
        {
            return new Dictionary<AllocatableAttributeName, int>();
        }

        var allocation = weights.ToDictionary(
            w => w.Attribute,
            w => totalPoints * w.Weight / totalWeight
        );

        var remainder = totalPoints - allocation.Values.Sum();
        if (remainder > 0)
        {
            var heaviestWeighted = weights.MaxBy(w => w.Weight).Attribute;
            allocation[heaviestWeighted] += remainder;
        }

        return allocation;
    }

    internal static Combatant GenerateMonster(
        GeneratedCombatantContext context,
        CreatureType creatureType,
        CreatureArchetype archetype,
        int level
    )
    {
        var input = new CreatureGeneratorInput(
            creatureType,
            archetype,
            Guid.NewGuid(),
            Guid.NewGuid(),
            level,
            level
        );
        var result = context.Generator.Generate(input);
        return GeneratedCombatantFactory.ToCombatant(result, isPlayer: false);
    }
}
