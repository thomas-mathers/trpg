using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public class HasPendingTrespassInBuildingQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid BuildingId { get; init; }
}

internal class HasPendingTrespassInBuildingQueryHandler(ICrimesDbContext context)
    : IQueryHandler<HasPendingTrespassInBuildingQuery, bool>
{
    public async Task<bool> Handle(
        HasPendingTrespassInBuildingQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Crimes.OfType<TrespassingCrime>()
            .AsNoTracking()
            .AnyAsync(
                crime =>
                    crime.PlayerId == query.PlayerId
                    && crime.BuildingId == query.BuildingId
                    && crime.Resolution == CrimeResolution.Pending,
                cancellationToken
            );
}
