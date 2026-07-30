using TRPG.Application.Balance;
using TRPG.Data.Models;

namespace TRPG.Balance;

// Runs real fights between a generated Profession and a generated monster CreatureType and
// prints every round's HP plus a plain-English description of what happened - for when the
// aggregate win-rate matrix and diagnose's static ability list aren't enough to explain a
// surprising result and the actual turn-by-turn sequence is needed instead.
internal static class TraceExperiment
{
    public static void Run(
        Profession profession,
        CreatureType monsterCreatureType,
        int level,
        int trialCount
    )
    {
        var context = GeneratedCombatantFactory.CreateContext();
        var simulator = new FightSimulator();
        var archetype = DiagnosticExperiment.MonsterArchetypeFor(monsterCreatureType);

        for (var trial = 1; trial <= trialCount; trial++)
        {
            var player = ProfessionMatrixExperiment.GeneratePlayer(context, profession, level);
            var enemy = ProfessionMatrixExperiment.GenerateMonster(
                context,
                monsterCreatureType,
                archetype,
                level
            );

            Console.WriteLine(
                $"=== Trial {trial}: {profession} (HP {player.MaximumHp}) vs "
                    + $"{monsterCreatureType} (HP {enemy.MaximumHp}) ==="
            );

            var snapshots = simulator.RunFight(trial, player, enemy);

            foreach (var snapshot in snapshots)
            {
                Console.WriteLine(
                    $"  Round {snapshot.Round}: player_hp={snapshot.PlayerHp} enemy_hp={snapshot.EnemyHp}"
                );
                foreach (var description in snapshot.EventDescriptions)
                {
                    Console.WriteLine($"    {description}");
                }
            }

            Console.WriteLine($"  Outcome: {snapshots[^1].Outcome}");
            Console.WriteLine();
        }
    }
}
