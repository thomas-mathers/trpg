using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetFactionsByIdsQuery
{
    public required IReadOnlyCollection<Guid> Ids { get; init; }
}

internal class GetFactionsByIdsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetFactionsByIdsQuery, IReadOnlyDictionary<Guid, Faction>>
{
    public async Task<IReadOnlyDictionary<Guid, Faction>> Handle(
        GetFactionsByIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Factions.AsNoTracking()
            .Where(faction => query.Ids.AsEnumerable().Contains(faction.Id))
            .ToDictionaryAsync(faction => faction.Id, cancellationToken);
}
