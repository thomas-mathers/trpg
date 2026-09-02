using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureIdsByDistrictQuery
{
    public required Guid WorldId { get; init; }
    public required Guid DistrictId { get; init; }
}

internal class GetCreatureIdsByDistrictQueryHandler(
    ICreaturesDbContext context,
    IQueryHandler<GetLocationIdsByDistrictQuery, IReadOnlyCollection<Guid>> getLocationIdsByDistrict
) : IQueryHandler<GetCreatureIdsByDistrictQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetCreatureIdsByDistrictQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var locationIds = await getLocationIdsByDistrict.Handle(
            new GetLocationIdsByDistrictQuery { DistrictId = query.DistrictId },
            cancellationToken
        );

        return await context
            .Creatures.Where(p =>
                p.WorldId == query.WorldId && locationIds.AsEnumerable().Contains(p.LocationId)
            )
            .Select(p => p.Id)
            .ToArrayAsync(cancellationToken);
    }
}
