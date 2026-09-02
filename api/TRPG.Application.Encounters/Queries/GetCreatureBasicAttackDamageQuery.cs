using TRPG.Application.Combat;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetCreatureBasicAttackDamageQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureBasicAttackDamageQueryHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    CombatantFactory combatantFactory,
    DamageCalculator damageCalculator
) : IQueryHandler<GetCreatureBasicAttackDamageQuery, float>
{
    public async Task<float> Handle(
        GetCreatureBasicAttackDamageQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creature =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = query.CreatureId },
                cancellationToken
            ) ?? throw new InvalidOperationException($"Creature {query.CreatureId} not found.");

        var combatant = await combatantFactory.Create(creature, isPlayer: true, cancellationToken);

        return damageCalculator.EstimateBasicAttackDamagePerTurn(combatant);
    }
}
