using System.Globalization;
using TRPG.Application.Balance;
using TRPG.Application.Configuration;
using TRPG.Data.Models;

namespace TRPG.Balance;

// Measures each playable profession's actual generated Defense+Evasion at a spread of levels,
// averages them per level, and back-solves the monster WeaponProficiency that would land a
// target hit chance against that average - see HitCalculator.CalculateHitChance for the formula
// this inverts. Used to pick a level -> monster-proficiency curve grounded in real generated
// stats instead of a guessed constant.
internal static class DefenseCurveExperiment
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

    private static readonly int[] Levels = [1, 3, 6, 9, 12, 20, 30, 50, 75, 100];

    public static async Task Run(double targetHitChance, string outputPath, int trialsPerCell = 30)
    {
        var context = GeneratedCombatantFactory.CreateContext();
        var combatOptions = new CombatOptions();

        await using var writer = new StreamWriter(outputPath);
        await writer.WriteLineAsync(
            "level,"
                + string.Join(',', PlayableProfessions)
                + ",average_defense_plus_evasion,suggested_monster_proficiency"
        );

        foreach (var level in Levels)
        {
            var defensePlusEvasionByProfession = PlayableProfessions
                .Select(profession =>
                {
                    var samples = Enumerable
                        .Range(0, trialsPerCell)
                        .Select(_ =>
                        {
                            var player = ProfessionMatrixExperiment.GeneratePlayer(
                                context,
                                profession,
                                level
                            );
                            var defense = player.CalculateEffectiveAttribute(AttributeName.Defense);
                            var evasion =
                                player.CalculateEffectiveAttribute(AttributeName.Dexterity)
                                * combatOptions.EvasionPerDexterityPoint;
                            return defense + evasion;
                        });
                    return samples.Average();
                })
                .ToArray();

            var average = defensePlusEvasionByProfession.Average();

            // Invert HitCalculator.CalculateHitChance's ratio: hitChance = proficiency / (proficiency + D)
            // => proficiency = hitChance * D / (1 - hitChance)
            var requiredProficiency = targetHitChance * average / (1 - targetHitChance);
            var suggestedWeaponProficiency = Math.Max(
                0,
                requiredProficiency - combatOptions.BaseProficiency
            );

            var row =
                $"{level},"
                + string.Join(
                    ',',
                    defensePlusEvasionByProfession.Select(v =>
                        v.ToString("F1", CultureInfo.InvariantCulture)
                    )
                )
                + $",{average.ToString("F1", CultureInfo.InvariantCulture)}"
                + $",{suggestedWeaponProficiency.ToString("F0", CultureInfo.InvariantCulture)}";

            await writer.WriteLineAsync(row);
            Console.WriteLine(row);
        }

        Console.WriteLine($"Wrote defense/evasion curve to {outputPath}");
    }
}
