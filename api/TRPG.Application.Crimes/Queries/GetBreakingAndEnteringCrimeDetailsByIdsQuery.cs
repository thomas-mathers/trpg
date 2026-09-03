using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public record BreakingAndEnteringCrimeDetails(Guid Id, Guid? OwnerFactionId);

public class GetBreakingAndEnteringCrimeDetailsByIdsQuery
{
    public required IReadOnlyCollection<Guid> CrimeIds { get; init; }
}

internal class GetBreakingAndEnteringCrimeDetailsByIdsQueryHandler(ICrimesDbContext context)
    : IQueryHandler<
        GetBreakingAndEnteringCrimeDetailsByIdsQuery,
        IReadOnlyDictionary<Guid, BreakingAndEnteringCrimeDetails>
    >
{
    public async Task<IReadOnlyDictionary<Guid, BreakingAndEnteringCrimeDetails>> Handle(
        GetBreakingAndEnteringCrimeDetailsByIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var crimeIds = query.CrimeIds;
        return await context
            .Crimes.OfType<BreakingAndEnteringCrime>()
            .AsNoTracking()
            .Where(crime => crimeIds.AsEnumerable().Contains(crime.Id))
            .Select(crime => new BreakingAndEnteringCrimeDetails(crime.Id, crime.OwnerFactionId))
            .ToDictionaryAsync(details => details.Id, cancellationToken);
    }
}
