using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureIdsByDistrictQuery
{
    public required Guid WorldId { get; init; }
    public required Guid DistrictId { get; init; }
}

internal class GetCreatureIdsByDistrictQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetCreatureIdsByDistrictQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetCreatureIdsByDistrictQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var ids = await context
            .Creatures.Where(p =>
                p.WorldId == query.WorldId
                && context.Locations.Any(l =>
                    l.Id == p.LocationId && l.DistrictId == query.DistrictId
                )
            )
            .Select(p => p.Id)
            .ToArrayAsync(cancellationToken);
        return ids;
    }
}
