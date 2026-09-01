using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;

namespace TRPG.Application.Worlds.Queries;

public class GetLocationIdsByDistrictQuery
{
    public required Guid DistrictId { get; init; }
}

internal class GetLocationIdsByDistrictQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetLocationIdsByDistrictQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetLocationIdsByDistrictQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Locations.AsNoTracking()
            .Where(location => location.DistrictId == query.DistrictId)
            .Select(location => location.Id)
            .ToArrayAsync(cancellationToken);
}
