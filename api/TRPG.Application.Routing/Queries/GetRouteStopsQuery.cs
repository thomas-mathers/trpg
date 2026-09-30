using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Routing.Queries;

public class GetRouteStopsQuery
{
    public required Guid RouteId { get; init; }
}

internal class GetRouteStopsQueryHandler(IRoutingDbContext context)
    : IQueryHandler<GetRouteStopsQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetRouteStopsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .RouteSteps.AsNoTracking()
            .Where(step => step.RouteId == query.RouteId && step.DwellHours > 0)
            .OrderBy(step => step.SequenceIndex)
            .Select(step => step.LocationId)
            .ToArrayAsync(cancellationToken);
}
