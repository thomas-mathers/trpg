using System.Globalization;
using TRPG.Application.Configuration;
using TRPG.Data.Models;

namespace TRPG.Balance;

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
                                level,
                                combatOptions
                            );
                            var defense = player.Defense;
                            var evasion = player.Evasion;
                            return defense + evasion;
                        });
                    return samples.Average();
                })
                .ToArray();

            var average = defensePlusEvasionByProfession.Average();
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
