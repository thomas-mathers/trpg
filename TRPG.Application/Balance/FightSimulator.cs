using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Data.Models;

namespace TRPG.Application.Balance;

public record RoundSnapshot(
    int Trial,
    int Round,
    string Outcome,
    int PlayerHp,
    int PlayerAp,
    int PlayerMp,
    int EnemyHp,
    int EnemyAp,
    int EnemyMp,
    IReadOnlyList<string> EventDescriptions
);

// Runs 1v1 fights between two directly-specified combatants to completion, for empirical
// balance testing. Both sides act through EnemyCombatActionResolver - one combatant is
// flagged IsPlayer purely so CombatEngine.ProcessRound's API is satisfied, but both use
// identical AI logic, so this measures the same decision-making real enemies already use.
public class FightSimulator
{
    private readonly CombatEngine _engine;
    private readonly EnemyCombatActionResolver _resolver;
    private readonly StatFormulas _statFormulas;

    public FightSimulator(
        CombatOptions? combatOptions = null,
        CreatureGeneratorOptions? creatureGeneratorOptions = null
    )
    {
        var combatOptionsSnapshot = new FixedOptionsSnapshot<CombatOptions>(
            combatOptions ?? new CombatOptions()
        );
        var hitCalculator = new HitCalculator(combatOptionsSnapshot);
        var damageCalculator = new DamageCalculator(combatOptionsSnapshot);
        _resolver = new EnemyCombatActionResolver(
            combatOptionsSnapshot,
            damageCalculator,
            hitCalculator
        );
        _engine = new CombatEngine(
            combatOptionsSnapshot,
            hitCalculator,
            damageCalculator,
            _resolver
        );
        _statFormulas = new StatFormulas(
            new FixedOptionsSnapshot<CreatureGeneratorOptions>(
                creatureGeneratorOptions ?? new CreatureGeneratorOptions()
            )
        );
    }

    // Every row in the returned list carries the fight's final outcome (not just the last
    // row), so trials can be filtered/grouped by winner without a separate lookup.
    public IReadOnlyList<RoundSnapshot> RunFight(
        int trial,
        CombatantSpec playerSpec,
        CombatantSpec enemySpec,
        int maxRounds = 300
    )
    {
        var player = SimulatedCombatantFactory.Build(playerSpec, isPlayer: true, _statFormulas);
        var enemy = SimulatedCombatantFactory.Build(enemySpec, isPlayer: false, _statFormulas);
        return RunFight(trial, player, enemy, maxRounds);
    }

    // Overload for combatants already built elsewhere (e.g. through the real CreatureGenerator
    // via GeneratedCombatantFactory) rather than a directly-specified CombatantSpec.
    public IReadOnlyList<RoundSnapshot> RunFight(
        int trial,
        Combatant player,
        Combatant enemy,
        int maxRounds = 300
    )
    {
        IReadOnlyList<Combatant> combatants = [player, enemy];

        var snapshots = new List<RoundSnapshot>();
        var round = 0;

        while (round < maxRounds)
        {
            round++;
            var playerAction = _resolver.Resolve(player, enemy);
            var state = _engine.ProcessRound(combatants, playerAction);

            snapshots.Add(
                new RoundSnapshot(
                    trial,
                    round,
                    state.Outcome.ToString(),
                    player.CurrentHp,
                    player.CurrentAp,
                    player.CurrentMp,
                    enemy.CurrentHp,
                    enemy.CurrentAp,
                    enemy.CurrentMp,
                    state.Events.Select(DescribeEvent).ToArray()
                )
            );

            if (state.Outcome != CombatOutcome.Ongoing)
            {
                break;
            }
        }

        var finalOutcome = snapshots[^1].Outcome;
        return snapshots.Select(s => s with { Outcome = finalOutcome }).ToArray();
    }

    private static string DescribeEvent(CombatEvent combatEvent) =>
        combatEvent switch
        {
            Hit hit =>
                $"{hit.AttackerName} hit {hit.TargetName} with {hit.AbilityName} for {hit.Damage} "
                    + $"({hit.TargetRemainingHp}/{hit.TargetMaximumHp} hp"
                    + (hit.Killed ? ", killed)" : ")"),
            Miss miss => $"{miss.AttackerName} missed {miss.TargetName} with {miss.AbilityName}",
            Block block => $"{block.TargetName} blocked {block.AttackerName}'s {block.AbilityName}",
            NoAction noAction => $"{noAction.CreatureName} could not act ({noAction.Condition})",
            DamageTicked tick =>
                $"{tick.CreatureName} took {tick.Damage} {tick.DamageType} from {tick.AbilityName} "
                    + $"({tick.RemainingHp}/{tick.MaximumHp} hp"
                    + (tick.Killed ? ", killed)" : ")"),
            BuffApplied buff =>
                $"{buff.SourceName} applied {buff.AbilityName} to {buff.TargetName}",
            Healed healed => $"{healed.SourceName} healed {healed.TargetName} for {healed.Amount} "
                + $"({healed.TargetRemainingHp}/{healed.TargetMaximumHp} hp)",
            HealOverTimeApplied hot =>
                $"{hot.SourceName} applied {hot.AbilityName} to {hot.TargetName}",
            ConsumedPotion potion => $"{potion.CreatureName} drank {potion.ItemName} "
                + $"({potion.RemainingValue}/{potion.MaximumValue} {potion.Resource})",
            _ => combatEvent.ToString() ?? "",
        };
}
