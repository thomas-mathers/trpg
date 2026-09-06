using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public record LockpickingCrimeDetails(
    Guid Id,
    Guid? OwnerFactionId,
    LockpickingCrimeOutcome? Outcome
);

public class GetLockpickingCrimeDetailsByIdsQuery
{
    public required IReadOnlyCollection<Guid> CrimeIds { get; init; }
}

internal class GetLockpickingCrimeDetailsByIdsQueryHandler(ICrimesDbContext context)
    : IQueryHandler<
        GetLockpickingCrimeDetailsByIdsQuery,
        IReadOnlyDictionary<Guid, LockpickingCrimeDetails>
    >
{
    public async Task<IReadOnlyDictionary<Guid, LockpickingCrimeDetails>> Handle(
        GetLockpickingCrimeDetailsByIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var crimeIds = query.CrimeIds;
        return await context
            .Crimes.OfType<LockpickingCrime>()
            .AsNoTracking()
            .Where(crime => crimeIds.AsEnumerable().Contains(crime.Id))
            .Select(crime => new LockpickingCrimeDetails(
                crime.Id,
                crime.OwnerFactionId,
                crime.Outcome
            ))
            .ToDictionaryAsync(details => details.Id, cancellationToken);
    }
}
