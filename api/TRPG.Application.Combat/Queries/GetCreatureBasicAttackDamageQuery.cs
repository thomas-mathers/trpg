using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;

namespace TRPG.Application.Combat.Queries;

public class GetCreatureBasicAttackDamageQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureBasicAttackDamageQueryHandler(
    TrpgDbContext context,
    CombatantFactory combatantFactory,
    DamageCalculator damageCalculator
) : IQueryHandler<GetCreatureBasicAttackDamageQuery, float>
{
    public async Task<float> Handle(
        GetCreatureBasicAttackDamageQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await context
            .Creatures.AsNoTracking()
            .FirstAsync(c => c.Id == query.CreatureId, cancellationToken);

        var combatant = await combatantFactory.Create(creature, isPlayer: true, cancellationToken);

        return damageCalculator.EstimateBasicAttackDamagePerTurn(combatant);
    }
}
