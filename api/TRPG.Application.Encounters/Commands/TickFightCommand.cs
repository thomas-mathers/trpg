using TRPG.Application.Combat;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;
using TRPG.Application.Encounters.Fights;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class TickFightCommand
{
    public required ActiveFight Fight { get; init; }
    public required GameInstant GameTime { get; init; }
    public required PlayerPlacement? PlayerPlacement { get; init; }
    public required Func<Guid, Point?> FindLiveCreaturePosition { get; init; }
    public Guid? AutoAttackTargetId { get; init; }
}

public record ChaseOrder(Guid CreatureId, Point Target, double StopDistance);

public record FightTickResult(bool Over, bool Changed, IReadOnlyList<ChaseOrder> Chases);

internal class TickFightCommandHandler(
    CombatEngine combatEngine,
    ICommandHandler<ResolveCombatRoundCommand, CombatResult> resolveCombatRound
) : ICommandHandler<TickFightCommand, FightTickResult>
{
    private const double StopDistanceFractionOfReach = 0.75;

    public async Task<FightTickResult> Handle(
        TickFightCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var fight = command.Fight;
        FightPlacement.Apply(
            fight.Combatants,
            command.PlayerPlacement,
            command.FindLiveCreaturePosition
        );

        var tick = combatEngine.TickFight(fight.Combatants, command.GameTime);
        if (tick.EffectsChanged)
        {
            await Resolve(command, tick, cancellationToken);
        }

        if (IsOver(tick))
        {
            return new FightTickResult(true, true, []);
        }

        var swing = await SwingForPlayer(command, cancellationToken);
        if (swing is { Over: true })
        {
            return new FightTickResult(true, true, []);
        }

        return await LetEnemiesAct(
            command,
            tick.EffectsChanged || swing is not null,
            cancellationToken
        );
    }

    private async Task<PlayerSwing?> SwingForPlayer(
        TickFightCommand command,
        CancellationToken cancellationToken
    )
    {
        if (command.AutoAttackTargetId is not { } targetId)
        {
            return null;
        }

        var fight = command.Fight;
        var player = fight.Combatants.Single(combatant => combatant.IsPlayer);
        var use = combatEngine.AutoAttack(
            fight.Combatants,
            player.CreatureId,
            targetId,
            command.GameTime
        );
        if (use.State is not { } state)
        {
            return null;
        }

        await Resolve(command, state, cancellationToken);

        return new PlayerSwing(IsOver(state));
    }

    private sealed record PlayerSwing(bool Over);

    private async Task<FightTickResult> LetEnemiesAct(
        TickFightCommand command,
        bool changed,
        CancellationToken cancellationToken
    )
    {
        var fight = command.Fight;
        var player = fight.Combatants.Single(combatant => combatant.IsPlayer);
        var chases = new List<ChaseOrder>();

        foreach (var enemy in fight.Combatants.Where(combatant => combatant is { IsPlayer: false }))
        {
            var use = combatEngine.ActAsEnemy(fight.Combatants, enemy.CreatureId, command.GameTime);
            if (use.State is { } state)
            {
                await Resolve(command, state, cancellationToken);
                changed = true;
                if (IsOver(state))
                {
                    return new FightTickResult(true, true, []);
                }
            }
            else if (use is { Rejection: AbilityUseRejection.OutOfRange, Reach: { } reach })
            {
                chases.Add(
                    new ChaseOrder(
                        enemy.CreatureId,
                        player.Position,
                        reach * StopDistanceFractionOfReach
                    )
                );
            }
        }

        return new FightTickResult(false, changed, chases);
    }

    private Task Resolve(
        TickFightCommand command,
        CombatState state,
        CancellationToken cancellationToken
    ) =>
        resolveCombatRound.Handle(
            new ResolveCombatRoundCommand
            {
                WorldId = command.Fight.WorldId,
                PlayerId = command.Fight.PlayerId,
                LocationId = command.Fight.LocationId,
                Combatants = command.Fight.Combatants,
                State = state,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

    private static bool IsOver(CombatState state) =>
        state.Outcome is CombatOutcome.Victory or CombatOutcome.Defeat or CombatOutcome.Fled;
}
