using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetAllBuildingsByStateIdQuery
{
    public required Guid StateId { get; init; }
}

internal class GetAllBuildingsByStateIdQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<Building>> Handle(
        GetAllBuildingsByStateIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var list = await context
            .Buildings.AsNoTracking()
            .Where(b => b.StateId == query.StateId)
            .ToArrayAsync(cancellationToken);
        return list;
    }
}
