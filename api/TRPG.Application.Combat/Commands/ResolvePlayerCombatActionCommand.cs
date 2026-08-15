using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Handling;
using TRPG.Contracts.Combat.Requests;

namespace TRPG.Application.Combat.Commands;

public class ResolvePlayerCombatActionCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required PlayerCombatAction Action { get; init; }
}

internal class ResolvePlayerCombatActionCommandHandler(
    IQueryHandler<GetActiveFightCombatantsQuery, IReadOnlyList<Combatant>> getCombatants,
    CombatEngine combatEngine,
    ICommandHandler<ResolveCombatRoundCommand, CombatResult> resolveCombatRound
) : ICommandHandler<ResolvePlayerCombatActionCommand, CombatResult>
{
    public async Task<CombatResult> Handle(
        ResolvePlayerCombatActionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var combatants = await getCombatants.Handle(
            new GetActiveFightCombatantsQuery { PlayerId = command.PlayerId },
            cancellationToken
        );

        if (combatants.Count == 0)
        {
            throw new InvalidOperationException("There's no fight to act in right now.");
        }

        var resolverResult = new PlayerCombatActionResolver(combatants).Resolve(command.Action);

        if (resolverResult.ErrorMessage is not null)
        {
            throw new InvalidOperationException(resolverResult.ErrorMessage);
        }

        var state = combatEngine.ProcessRound(combatants, resolverResult.Result!);

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
