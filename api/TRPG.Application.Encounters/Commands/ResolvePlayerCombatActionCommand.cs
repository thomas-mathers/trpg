using TRPG.Application.Combat;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ResolvePlayerCombatActionCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required PlayerCombatAction Action { get; init; }
}

public record PlayerCombatActionResult(
    CombatResult CombatResult,
    IReadOnlyCollection<string> OpponentNames
);

internal class ResolvePlayerCombatActionCommandHandler(
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IQueryHandler<GetActiveFightQuery, FightEncounter?> getActiveFight,
    ActiveFightCombatantLoader combatantLoader,
    CombatEngine combatEngine,
    ICommandHandler<ResolveCombatRoundCommand, CombatResult> resolveCombatRound,
    ICommandHandler<IncrementFightEncounterRoundCommand> incrementFightEncounterRound
) : ICommandHandler<ResolvePlayerCombatActionCommand, PlayerCombatActionResult>
{
    public async Task<PlayerCombatActionResult> Handle(
        ResolvePlayerCombatActionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand { Playtime = playtime, CreatureIds = [command.PlayerId] },
            cancellationToken
        );
        var combatants = await combatantLoader.Load(command.PlayerId, cancellationToken);
        if (combatants.Count == 0)
            throw new InvalidOperationException("There's no fight to act in right now.");

        var fight = await getActiveFight.Handle(
            new GetActiveFightQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        var isSurpriseRound = fight is { RoundsResolved: 0, HasSurpriseRound: true };
        if (isSurpriseRound)
        {
            combatants.Single(c => c.IsPlayer).IsSurpriseAttacker = true;
        }

        var resolved = new PlayerCombatActionResolver(combatants).Resolve(command.Action);
        if (resolved.ErrorMessage is not null)
            throw new InvalidOperationException(resolved.ErrorMessage);
        var state = combatEngine.ProcessRound(combatants, resolved.Result!, isSurpriseRound);
        var combatResult = await resolveCombatRound.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                Combatants = combatants,
                State = state,
            },
            cancellationToken
        );
        await incrementFightEncounterRound.Handle(
            new IncrementFightEncounterRoundCommand { FightEncounterId = fight!.Id },
            cancellationToken
        );

        var opponentNames = combatants
            .Where(combatant => !combatant.IsPlayer)
            .Select(combatant => combatant.Name)
            .ToArray();

        return new PlayerCombatActionResult(combatResult, opponentNames);
    }
}
