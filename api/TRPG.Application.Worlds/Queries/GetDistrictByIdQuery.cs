using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetDistrictByIdQuery
{
    public required Guid Id { get; init; }
}

internal class GetDistrictByIdQueryHandler(TrpgDbContext context, IMemoryCache cache)
    : IQueryHandler<GetDistrictByIdQuery, District?>
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
