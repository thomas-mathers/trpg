using TRPG.Application.Combat.Mappers;
using TRPG.Application.Common.Handling;
using CombatantDetail = TRPG.Contracts.Combat.Responses.CombatantState;

namespace TRPG.Application.Combat.Queries;

public class GetActiveFightCombatantDetailsQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetActiveFightCombatantDetailsQueryHandler(
    IQueryHandler<GetActiveFightCombatantsQuery, IReadOnlyList<Combatant>> getActiveFightCombatants
) : IQueryHandler<GetActiveFightCombatantDetailsQuery, IReadOnlyCollection<CombatantDetail>>
{
    public async Task<IReadOnlyCollection<CombatantDetail>> Handle(
        GetActiveFightCombatantDetailsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var combatants = await getActiveFightCombatants.Handle(
            new GetActiveFightCombatantsQuery { PlayerId = query.PlayerId },
            cancellationToken
        );

        return CombatantStateMapper.ToCombatantStates(combatants);
    }
}
