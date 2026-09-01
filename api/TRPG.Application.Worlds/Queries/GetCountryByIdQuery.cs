using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetCountryByIdQuery
{
    public required Guid Id { get; init; }
}

internal class GetCountryByIdQueryHandler(TrpgDbContext context, IMemoryCache cache)
    : IQueryHandler<GetCountryByIdQuery, Country?>
{
    public async Task<Country?> Handle(
        GetCountryByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrCreateAsync(
            $"country:{query.Id}",
            _ =>
                context
                    .Countries.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == query.Id, cancellationToken)
        );
    }
}
