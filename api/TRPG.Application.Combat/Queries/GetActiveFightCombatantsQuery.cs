using TRPG.Application.Combat.Extensions;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Queries;

namespace TRPG.Application.Combat.Queries;

public class GetActiveFightCombatantsQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetActiveFightCombatantsQueryHandler(ActiveFightCombatantLoader combatantLoader)
    : IQueryHandler<GetActiveFightCombatantsQuery, IReadOnlyList<CombatantResult>>
{
    public async Task<IReadOnlyList<CombatantResult>> Handle(
        GetActiveFightCombatantsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var combatants = await combatantLoader.Load(query.PlayerId, cancellationToken);
        return combatants
            .OrderByDescending(combatant => combatant.TurnOrder)
            .Select(combatant => combatant.ToCombatantResult())
            .ToArray();
    }
}
