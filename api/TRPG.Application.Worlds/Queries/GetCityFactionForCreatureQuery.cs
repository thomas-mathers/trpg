using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;

namespace TRPG.Application.Worlds.Queries;

public class GetCityFactionForCreatureQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCityFactionForCreatureQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetCityFactionForCreatureQuery, Guid?>
{
    public async Task<Guid?> Handle(
        GetCityFactionForCreatureQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await (
            from member in context.FactionMembers.AsNoTracking()
            join faction in context.Factions.AsNoTracking() on member.FactionId equals faction.Id
            where member.CreatureId == query.CreatureId && faction.IsCityFaction
            select (Guid?)faction.Id
        ).FirstOrDefaultAsync(cancellationToken);
    }
}
