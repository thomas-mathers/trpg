using TRPG.Application.Creatures.Queries;

namespace TRPG.Application.Combat.Queries;

internal class GetActiveFightCombatantsQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetActiveFightCombatantsQueryHandler(
    GetActiveFightQueryHandler getActiveFight,
    GetCreaturesByIdsQueryHandler getCreaturesByIds,
    GetCombatantQueryHandler getCombatant
)
{
    public async Task<IReadOnlyList<Combatant>> Handle(
        GetActiveFightCombatantsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var fight = await getActiveFight.Handle(
            new GetActiveFightQuery { PlayerId = query.PlayerId },
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
            var isPlayer = creature.Id == query.PlayerId;

            var combatant = await getCombatant.Handle(
                new GetCombatantQuery { Creature = creature, IsPlayer = isPlayer },
                cancellationToken
            );

            combatants.Add(combatant);
        }

        return combatants;
    }
}
