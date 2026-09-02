using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetBuildingsByIdsQuery
{
    public required IReadOnlyCollection<Guid> Ids { get; init; }
}

internal class GetBuildingsByIdsQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetBuildingsByIdsQuery, IReadOnlyDictionary<Guid, Building>>
{
    public async Task<IReadOnlyDictionary<Guid, Building>> Handle(
        GetBuildingsByIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Buildings.AsNoTracking()
            .Where(building => query.Ids.AsEnumerable().Contains(building.Id))
            .ToDictionaryAsync(building => building.Id, cancellationToken);
}
