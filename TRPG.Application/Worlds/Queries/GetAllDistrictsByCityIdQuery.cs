using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

internal class GetAllDistrictsByCityIdQuery
{
    public required Guid CityId { get; init; }
}

internal class GetAllDistrictsByCityIdQueryHandler(TrpgDbContext context, IMemoryCache cache)
{
    public async Task<IReadOnlyCollection<District>> Handle(
        GetAllDistrictsByCityIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var districts = await cache.GetOrCreateAsync<District[]>(
            $"districts:{query.CityId}",
            async _ =>
                await context
                    .Districts.AsNoTracking()
                    .Where(d => d.CityId == query.CityId)
                    .ToArrayAsync(cancellationToken)
        );
        return districts ?? [];
    }
}
