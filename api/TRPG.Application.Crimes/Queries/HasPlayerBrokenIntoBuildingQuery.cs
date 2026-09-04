using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public class HasPlayerBrokenIntoBuildingQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid BuildingId { get; init; }
}

internal class HasPlayerBrokenIntoBuildingQueryHandler(ICrimesDbContext context)
    : IQueryHandler<HasPlayerBrokenIntoBuildingQuery, bool>
{
    public async Task<bool> Handle(
        HasPlayerBrokenIntoBuildingQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Crimes.OfType<BreakingAndEnteringCrime>()
            .AsNoTracking()
            .AnyAsync(
                crime => crime.PlayerId == query.PlayerId && crime.BuildingId == query.BuildingId,
                cancellationToken
            );
}
