using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat;

internal class ActiveFightCombatantLoader(
    IQueryHandler<GetActiveFightQuery, Fight?> getActiveFight,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    CombatantFactory combatantFactory
)
{
    public async Task<IReadOnlyList<Combatant>> Load(
        Guid playerId,
        CancellationToken cancellationToken = default
    )
    {
        var fight = await getActiveFight.Handle(
            new GetActiveFightQuery { PlayerId = playerId },
            cancellationToken
        );
        if (fight == null)
        {
            return [];
        }

        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = fight.CombatantIds },
            cancellationToken
        );

        var combatants = new List<Combatant>();
        foreach (var creature in fight.CombatantIds.Select(id => creaturesById[id]))
        {
            combatants.Add(
                await combatantFactory.Create(creature, creature.Id == playerId, cancellationToken)
            );
        }

        return combatants;
    }
}
