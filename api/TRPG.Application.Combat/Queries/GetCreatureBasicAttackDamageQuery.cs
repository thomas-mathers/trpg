using Microsoft.EntityFrameworkCore;
using TRPG.Application.Combat;
using TRPG.Application.Common.Handling;
using TRPG.Data;

namespace TRPG.Application.Combat.Queries;

public class GetCreatureBasicAttackDamageQuery
{
    public required Guid CreatureId { get; init; }
}

public class GetCreatureBasicAttackDamageQueryHandler(
    TrpgDbContext context,
    IQueryHandler<GetCombatantQuery, Combatant> getCombatant,
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

        var combatant = await getCombatant.Handle(
            new GetCombatantQuery { Creature = creature, IsPlayer = true },
            cancellationToken
        );

        return damageCalculator.EstimateBasicAttackDamagePerTurn(combatant);
    }
}
