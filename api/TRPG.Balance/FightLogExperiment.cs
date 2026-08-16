using TRPG.Domain.Models;

namespace TRPG.Balance;

internal static class FightLogExperiment
{
    public static async Task Run(int trialCount, string outputPath)
    {
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
