using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public record UnsettledJailbreak(Guid CrimeId, string BuildingName);

public class GetUnsettledJailbreakQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class GetUnsettledJailbreakQueryHandler(ICrimesDbContext context)
    : IQueryHandler<GetUnsettledJailbreakQuery, UnsettledJailbreak?>
{
    public Task<UnsettledJailbreak?> Handle(
        GetUnsettledJailbreakQuery query,
        CancellationToken cancellationToken = default
    ) =>
        context
            .Crimes.OfType<JailbreakCrime>()
            .AsNoTracking()
            .Where(crime =>
                crime.WorldId == query.WorldId
                && crime.PlayerId == query.PlayerId
                && crime.Resolution == CrimeResolution.Pending
                && crime.SettledAt == null
            )
            .OrderByDescending(crime => crime.OccurredAt)
            .Select(crime => new UnsettledJailbreak(crime.Id, crime.BuildingName))
            .FirstOrDefaultAsync(cancellationToken);
}
