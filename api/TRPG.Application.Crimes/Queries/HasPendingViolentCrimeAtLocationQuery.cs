using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public class HasPendingViolentCrimeAtLocationQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class HasPendingViolentCrimeAtLocationQueryHandler(ICrimesDbContext context)
    : IQueryHandler<HasPendingViolentCrimeAtLocationQuery, bool>
{
    public async Task<bool> Handle(
        HasPendingViolentCrimeAtLocationQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Crimes.AsNoTracking()
            .AnyAsync(
                crime =>
                    (crime is AssaultCrime || crime is KillCrime)
                    && crime.WorldId == query.WorldId
                    && crime.PlayerId == query.PlayerId
                    && crime.LocationId == query.LocationId
                    && crime.Resolution == CrimeResolution.Pending,
                cancellationToken
            );
}
