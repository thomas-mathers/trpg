using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public record TheftCrimeDetails(Guid Id, Guid? OwnerFactionId, TheftCrimeOutcome? Outcome);

public class GetTheftCrimeDetailsByIdsQuery
{
    public required IReadOnlyCollection<Guid> CrimeIds { get; init; }
}

internal class GetTheftCrimeDetailsByIdsQueryHandler(ICrimesDbContext context)
    : IQueryHandler<GetTheftCrimeDetailsByIdsQuery, IReadOnlyDictionary<Guid, TheftCrimeDetails>>
{
    public async Task<IReadOnlyDictionary<Guid, TheftCrimeDetails>> Handle(
        GetTheftCrimeDetailsByIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var crimeIds = query.CrimeIds;
        return await context
            .Crimes.OfType<TheftCrime>()
            .AsNoTracking()
            .Where(crime => crimeIds.AsEnumerable().Contains(crime.Id))
            .Select(crime => new TheftCrimeDetails(crime.Id, crime.OwnerFactionId, crime.Outcome))
            .ToDictionaryAsync(details => details.Id, cancellationToken);
    }
}
