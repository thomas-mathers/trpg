using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetStaticPropsByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetStaticPropsByLocationIdQueryHandler(TrpgDbContext context, IMemoryCache cache)
{
    public async Task<IReadOnlyCollection<Prop>> Handle(
        GetStaticPropsByLocationIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var props = await cache.GetOrCreateAsync<Prop[]>(
            $"staticProps:{query.LocationId}",
            async _ =>
                (
                    await context
                        .Props.AsNoTracking()
                        .Where(p => p.LocationId == query.LocationId)
                        .ToArrayAsync(cancellationToken)
                )
                    .Where(p => p is not LocationConnector)
                    .ToArray()
        );
        return props ?? [];
    }
}
