using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Factions.Queries;

public class GetFactionsByCreatureTypeQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetFactionsByCreatureTypeQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetFactionsByCreatureTypeQuery, IReadOnlyDictionary<CreatureType, Faction>>
{
    public async Task<IReadOnlyDictionary<CreatureType, Faction>> Handle(
        GetFactionsByCreatureTypeQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var factions = await context
            .Factions.AsNoTracking()
            .Where(f => f.WorldId == query.WorldId && f.CreatureType != null)
            .ToListAsync(cancellationToken);

        return factions.ToDictionary(f => f.CreatureType!.Value, f => f);
    }
}
