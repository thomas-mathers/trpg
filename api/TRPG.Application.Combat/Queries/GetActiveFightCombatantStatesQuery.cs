using TRPG.Application.Combat.Mappers;
using TRPG.Application.Common.Handling;
using ContractCombatantState = TRPG.Contracts.Combat.Responses.CombatantState;

namespace TRPG.Application.Combat.Queries;

public class GetActiveFightCombatantStatesQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetActiveFightCombatantStatesQueryHandler(
    IQueryHandler<GetActiveFightCombatantsQuery, IReadOnlyList<Combatant>> getCombatants
) : IQueryHandler<GetActiveFightCombatantStatesQuery, IReadOnlyCollection<ContractCombatantState>>
{
    public async Task<IReadOnlyCollection<ContractCombatantState>> Handle(
        GetActiveFightCombatantStatesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var combatants = await getCombatants.Handle(
            new GetActiveFightCombatantsQuery { PlayerId = query.PlayerId },
            cancellationToken
        );
        return combatants.ToCombatantStates();
    }
}
