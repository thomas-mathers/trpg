using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat;

internal class ActiveFightCombatantLoader(
    IQueryHandler<GetActiveFightQuery, FightEncounter?> getActiveFight,
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

        var creatures = fight.CombatantIds.Select(id => creaturesById[id]).ToArray();

        return await combatantFactory.CreateMany(
            fight.WorldId,
            creatures,
            playerId,
            cancellationToken
        );
    }
}
