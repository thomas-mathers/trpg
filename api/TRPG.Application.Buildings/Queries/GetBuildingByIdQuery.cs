using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

public class GetBuildingByIdQuery
{
    public required Guid Id { get; init; }
}

internal class GetBuildingByIdQueryHandler(TrpgDbContext context, IMemoryCache cache)
    : IQueryHandler<GetBuildingByIdQuery, Building?>
{
    public async Task<Building?> Handle(
        GetBuildingByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrCreateAsync(
            $"building:{query.Id}",
            _ =>
                context
                    .Buildings.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == query.Id, cancellationToken)
        );
    }
}
