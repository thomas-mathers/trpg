using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetCityByStateIdQuery
{
    public required Guid StateId { get; init; }
}

public class GetCityByStateIdQueryHandler(TrpgDbContext context, IMemoryCache cache)
{
    public async Task<City?> Handle(
        GetCityByStateIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrCreateAsync(
            $"cityByState:{query.StateId}",
            _ =>
                context
                    .Cities.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.StateId == query.StateId, cancellationToken)
        );
    }
}
