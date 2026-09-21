using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetCitiesByIdsQuery
{
    public required IReadOnlyCollection<Guid> Ids { get; init; }
}

internal class GetCitiesByIdsQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetCitiesByIdsQuery, IReadOnlyDictionary<Guid, City>>
{
    public async Task<IReadOnlyDictionary<Guid, City>> Handle(
        GetCitiesByIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Cities.AsNoTracking()
            .Where(city => query.Ids.AsEnumerable().Contains(city.Id))
            .ToDictionaryAsync(city => city.Id, cancellationToken);
}
