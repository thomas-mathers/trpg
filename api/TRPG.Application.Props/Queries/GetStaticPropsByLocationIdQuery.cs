using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetStaticPropsByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetStaticPropsByLocationIdQueryHandler(TrpgDbContext context, IMemoryCache cache)
    : IQueryHandler<GetStaticPropsByLocationIdQuery, IReadOnlyCollection<Prop>>
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
                ).ToArray()
        );
        return props ?? [];
    }
}
