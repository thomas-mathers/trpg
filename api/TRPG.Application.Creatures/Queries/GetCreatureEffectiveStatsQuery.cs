using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureEffectiveStatsQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureEffectiveStatsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetCreatureEffectiveStatsQuery, Attributes>
{
    public async Task<Attributes> Handle(
        GetCreatureEffectiveStatsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await context
            .Creatures.AsNoTracking()
            .FirstAsync(c => c.Id == query.CreatureId, cancellationToken);

        return new Attributes
        {
            Strength = creature.Strength,
            Dexterity = creature.Dexterity,
            Intelligence = creature.Intelligence,
            Endurance = creature.Endurance,
            Stamina = creature.Stamina,
            Mana = creature.Mana,
            Defense = creature.Defense,
            MaximumHp = creature.MaximumHp,
            MaximumAp = creature.MaximumAp,
            MaximumMp = creature.MaximumMp,
            MovementSpeed = creature.MovementSpeed,
            PhysicalResistance = creature.PhysicalResistance,
            FireResistance = creature.FireResistance,
            IceResistance = creature.IceResistance,
            LightningResistance = creature.LightningResistance,
            PoisonResistance = creature.PoisonResistance,
            MagicResistance = creature.MagicResistance,
        };
    }
}
