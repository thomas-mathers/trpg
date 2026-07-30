using TRPG.Data.Models;

namespace TRPG.Balance;

// Runs repeated 1v1 fights between two directly-specified CombatantSpecs and logs every
// round to CSV - a sanity-check tool for the simulator itself (e.g. a symmetric matchup
// should land close to a 50/50 win rate) and for probing specific balance questions.
internal static class FightLogExperiment
{
    public static async Task Run(int trialCount, string outputPath)
    {
        // Default: two identical level-5-Melee combatants, as a sanity check for the tool
        // itself - a fair mirror match should land close to a 50/50 win rate. Swap these
        // specs out to test real balance questions (level gaps, skill investment, etc.).
        var attributes = new Attributes
        {
            Strength = 12,
            Dexterity = 10,
            Intelligence = 8,
            Endurance = 10,
            Stamina = 10,
            Defense = 5,
            Mana = 5,
        };

        var playerSpec = new CombatantSpec(
            "Player",
            attributes,
            new Dictionary<Skill, int> { [Skill.Melee] = 5 }
        );

        var enemySpec = new CombatantSpec(
            "Enemy",
            attributes,
            new Dictionary<Skill, int> { [Skill.Melee] = 5 }
        );

        var simulator = new FightSimulator();

        await using var writer = new StreamWriter(outputPath);
        await writer.WriteLineAsync(
            "trial,round,outcome,player_hp,player_ap,player_mp,enemy_hp,enemy_ap,enemy_mp"
        );

        for (var trial = 1; trial <= trialCount; trial++)
        {
            var snapshots = simulator.RunFight(trial, playerSpec, enemySpec);
            foreach (var snapshot in snapshots)
            {
                await writer.WriteLineAsync(
                    $"{snapshot.Trial},{snapshot.Round},{snapshot.Outcome},{snapshot.PlayerHp},"
                        + $"{snapshot.PlayerAp},{snapshot.PlayerMp},{snapshot.EnemyHp},{snapshot.EnemyAp},"
                        + $"{snapshot.EnemyMp}"
                );
            }
        }

        Console.WriteLine($"Wrote {trialCount} trials to {outputPath}");
    }
}
