using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureIdsByDistrictQuery
{
    public required Guid WorldId { get; init; }
    public required Guid DistrictId { get; init; }
}

internal class GetCreatureIdsByDistrictQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetCreatureIdsByDistrictQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var ids = await context
            .Creatures.Where(p => p.WorldId == query.WorldId && p.DistrictId == query.DistrictId)
            .Select(p => p.Id)
            .ToArrayAsync(cancellationToken);
        return ids;
    }
}
