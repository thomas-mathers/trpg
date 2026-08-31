using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetAllPropsByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetAllPropsByLocationIdQueryHandler(TrpgDbContext context, IMemoryCache cache)
    : IQueryHandler<GetAllPropsByLocationIdQuery, IReadOnlyCollection<Prop>>
{
    public async Task<IReadOnlyCollection<Prop>> Handle(
        GetAllPropsByLocationIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var props = await cache.GetOrCreateAsync<Prop[]>(
            $"allProps:{query.LocationId}",
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
