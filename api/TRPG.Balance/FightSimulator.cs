using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;
using TRPG.Application.Configuration;
using TRPG.Application.Effects;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Balance;

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

public class FightSimulator
{
    private const double FightDistance = 2.0;
    private static readonly GameInstant StartTime = new(
        new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
    );

    private readonly CombatEngine _engine;
    private readonly EnemyCombatActionResolver _resolver;
    private readonly CreatureGeneratorOptions _creatureGeneratorOptions;
    private readonly CombatOptions _combatOptions;

    public FightSimulator(
        CombatOptions? combatOptions = null,
        CreatureGeneratorOptions? creatureGeneratorOptions = null
    )
    {
        _combatOptions = combatOptions ?? new CombatOptions();
        var combatOptionsSnapshot = new FixedOptionsSnapshot<CombatOptions>(_combatOptions);
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
            _resolver,
            new EffectAdvancer(combatOptionsSnapshot, EffectTimeScale.Unscaled),
            EffectTimeScale.Unscaled
        );
        _creatureGeneratorOptions = creatureGeneratorOptions ?? new CreatureGeneratorOptions();
    }

    internal IReadOnlyList<RoundSnapshot> RunFight(
        int trial,
        CombatantSpec playerSpec,
        CombatantSpec enemySpec,
        int maxRounds = 300
    )
    {
        var player = SimulatedCombatantFactory.Build(
            playerSpec,
            isPlayer: true,
            _creatureGeneratorOptions,
            _combatOptions
        );
        var enemy = SimulatedCombatantFactory.Build(
            enemySpec,
            isPlayer: false,
            _creatureGeneratorOptions,
            _combatOptions
        );
        return RunFight(trial, player, enemy, maxRounds);
    }

    internal IReadOnlyList<RoundSnapshot> RunFight(
        int trial,
        Combatant player,
        Combatant enemy,
        int maxRounds = 300
    )
    {
        IReadOnlyList<Combatant> combatants = [player, enemy];
        Place(player, enemy);
        _engine.BeginFight(combatants, StartTime);

        var snapshots = new List<RoundSnapshot>();
        var round = 0;
        var now = StartTime;

        while (round < maxRounds)
        {
            round++;
            now += CombatTiming.Round;
            var state = ResolveRound(combatants, player, enemy, now);

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

    private CombatState ResolveRound(
        IReadOnlyList<Combatant> combatants,
        Combatant player,
        Combatant enemy,
        GameInstant now
    )
    {
        var events = new List<CombatResolution>();
        var tick = _engine.TickFight(combatants, now);
        events.AddRange(tick.Events);
        var state = tick;

        if (tick.Outcome == CombatOutcome.Ongoing)
        {
            var playerUse = ActAsPlayer(combatants, player, enemy, now);
            if (playerUse.State is { } playerState)
            {
                events.AddRange(playerState.Events);
                state = playerState;
            }
        }

        if (state.Outcome == CombatOutcome.Ongoing)
        {
            var enemyUse = _engine.ActAsEnemy(combatants, enemy.CreatureId, now);
            if (enemyUse.State is { } enemyState)
            {
                events.AddRange(enemyState.Events);
                state = enemyState;
            }
        }

        return state with
        {
            Events = events,
        };
    }

    private AbilityUseResult ActAsPlayer(
        IReadOnlyList<Combatant> combatants,
        Combatant player,
        Combatant enemy,
        GameInstant now
    )
    {
        var action = _resolver.Resolve(player, enemy, now);

        return action switch
        {
            ResolvedUseAbilityAction resolved => _engine.UseAbility(
                combatants,
                player.CreatureId,
                resolved.Targets[0].CreatureId,
                resolved.Ability.Name,
                now
            ),
            ResolvedUseItemAction resolved => _engine.UseItem(
                combatants,
                player.CreatureId,
                resolved.Item.ItemId,
                now
            ),
            _ => AbilityUseResult.Rejected(AbilityUseRejection.NotReady, "Nothing to do."),
        };
    }

    private static void Place(Combatant player, Combatant enemy)
    {
        player.Position = new Point(0, 0);
        enemy.Position = new Point(0, -FightDistance);
        player.Heading = AbilityFacing.HeadingToward(player.Position, enemy.Position);
        enemy.Heading = AbilityFacing.HeadingToward(enemy.Position, player.Position);
    }

    private static string DescribeEvent(CombatResolution combatEvent) =>
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
