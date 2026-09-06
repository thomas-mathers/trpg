using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public record TrespassingCrimeDetails(Guid Id, Guid? OwnerFactionId);

public class GetTrespassingCrimeDetailsByIdsQuery
{
    public required IReadOnlyCollection<Guid> CrimeIds { get; init; }
}

internal class GetTrespassingCrimeDetailsByIdsQueryHandler(ICrimesDbContext context)
    : IQueryHandler<
        GetTrespassingCrimeDetailsByIdsQuery,
        IReadOnlyDictionary<Guid, TrespassingCrimeDetails>
    >
{
    public async Task<IReadOnlyDictionary<Guid, TrespassingCrimeDetails>> Handle(
        GetTrespassingCrimeDetailsByIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var crimeIds = query.CrimeIds;
        return await context
            .Crimes.OfType<TrespassingCrime>()
            .AsNoTracking()
            .Where(crime => crimeIds.AsEnumerable().Contains(crime.Id))
            .Select(crime => new TrespassingCrimeDetails(crime.Id, crime.OwnerFactionId))
            .ToDictionaryAsync(details => details.Id, cancellationToken);
    }
}
