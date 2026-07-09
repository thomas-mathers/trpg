using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetBuildingByNameInStateQuery
{
    public required Guid StateId { get; init; }
    public required string Name { get; init; }
}

internal class GetBuildingByNameInStateQueryHandler(TrpgDbContext context)
{
    public async Task<Building?> Handle(
        GetBuildingByNameInStateQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Buildings.AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.StateId == query.StateId && EF.Functions.ILike(b.Name, query.Name),
                cancellationToken
            );
    }
}
