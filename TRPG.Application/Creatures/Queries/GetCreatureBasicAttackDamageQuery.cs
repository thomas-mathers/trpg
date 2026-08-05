using Microsoft.EntityFrameworkCore;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Queries;
using TRPG.Data;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureBasicAttackDamageQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureBasicAttackDamageQueryHandler(
    TrpgDbContext context,
    GetCombatantQueryHandler getCombatant,
    DamageCalculator damageCalculator
)
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
