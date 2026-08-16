using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetCityByIdQuery
{
    public required Guid Id { get; init; }
}

internal class GetCityByIdQueryHandler(TrpgDbContext context, IMemoryCache cache)
    : IQueryHandler<GetCityByIdQuery, City?>
{
    public async Task<City?> Handle(
        GetCityByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrCreateAsync(
            $"city:{query.Id}",
            _ =>
                context
                    .Cities.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == query.Id, cancellationToken)
        );
    }
}
