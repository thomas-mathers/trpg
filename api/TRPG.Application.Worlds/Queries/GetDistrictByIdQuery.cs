using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetDistrictByIdQuery
{
    public required Guid Id { get; init; }
}

public class GetDistrictByIdQueryHandler(TrpgDbContext context, IMemoryCache cache)
{
    public async Task<District?> Handle(
        GetDistrictByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrCreateAsync(
            $"district:{query.Id}",
            _ =>
                context
                    .Districts.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == query.Id, cancellationToken)
        );
    }
}
