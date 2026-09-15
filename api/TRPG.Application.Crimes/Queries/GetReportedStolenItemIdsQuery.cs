using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

// Items is a jsonb-converted column (see TrpgDbContext's TheftCrime configuration), so it can't be
// filtered in SQL — narrow by the translatable columns first, then check the deserialized items.
public class GetReportedStolenItemIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required IReadOnlyCollection<Guid> ItemIds { get; init; }
}

internal class GetReportedStolenItemIdsQueryHandler(ICrimesDbContext context)
    : IQueryHandler<GetReportedStolenItemIdsQuery, IReadOnlySet<Guid>>
{
    public async Task<IReadOnlySet<Guid>> Handle(
        GetReportedStolenItemIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var reportedThefts = await context
            .Crimes.AsNoTracking()
            .OfType<TheftCrime>()
            .Where(crime =>
                crime.WorldId == query.WorldId
                && crime.PlayerId == query.PlayerId
                && crime.Resolution == CrimeResolution.Reported
            )
            .ToArrayAsync(cancellationToken);

        return reportedThefts
            .SelectMany(crime => crime.Items)
            .Select(item => item.ItemId)
            .Where(itemId => query.ItemIds.Contains(itemId))
            .ToHashSet();
    }
}
