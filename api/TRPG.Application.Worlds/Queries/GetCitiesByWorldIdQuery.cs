using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetCitiesByWorldIdQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetCitiesByWorldIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetCitiesByWorldIdQuery, IReadOnlyCollection<City>>
{
    public async Task<IReadOnlyCollection<City>> Handle(
        GetCitiesByWorldIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Cities.AsNoTracking()
            .Where(city => city.WorldId == query.WorldId)
            .ToArrayAsync(cancellationToken);
}
