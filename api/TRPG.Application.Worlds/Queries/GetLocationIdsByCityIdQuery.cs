using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetLocationIdsByCityIdQuery
{
    public required Guid CityId { get; init; }
}

internal class GetLocationIdsByCityIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetLocationIdsByCityIdQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetLocationIdsByCityIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Locations.AsNoTracking()
            .Where(l => l.CityId == query.CityId)
            .Select(l => l.Id)
            .ToArrayAsync(cancellationToken);
}
