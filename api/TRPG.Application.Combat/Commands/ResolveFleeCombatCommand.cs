using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Handling;

namespace TRPG.Application.Combat.Commands;

public class ResolveFleeCombatCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class ResolveFleeCombatCommandHandler(
    IQueryHandler<GetActiveFightCombatantsQuery, IReadOnlyList<Combatant>> getCombatants,
    CombatEngine combatEngine,
    ICommandHandler<ResolveCombatRoundCommand, CombatResult> resolveCombatRound
) : ICommandHandler<ResolveFleeCombatCommand, CombatResult?>
{
    public async Task<CombatResult?> Handle(
        ResolveFleeCombatCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var combatants = await getCombatants.Handle(
            new GetActiveFightCombatantsQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (combatants.Count == 0)
            return null;
        var state = combatEngine.ResolveFlee(combatants);
        return await resolveCombatRound.Handle(
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
    }
}
