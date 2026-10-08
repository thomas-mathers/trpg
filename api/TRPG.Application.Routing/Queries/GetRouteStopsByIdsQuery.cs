using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Routing.Queries;

public class GetRouteStopsByIdsQuery
{
    public required IReadOnlyCollection<Guid> RouteIds { get; init; }
}

internal class GetRouteStopsByIdsQueryHandler(IRoutingDbContext context)
    : IQueryHandler<GetRouteStopsByIdsQuery, IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> Handle(
        GetRouteStopsByIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var routeIds = query.RouteIds.ToArray();
        var steps = await context
            .RouteSteps.AsNoTracking()
            .Where(step => routeIds.Contains(step.RouteId))
            .OrderBy(step => step.SequenceIndex)
            .Where(step => step.TravelNodeId != null)
            .Select(step => new { step.RouteId, step.TravelNodeId })
            .ToArrayAsync(cancellationToken);

        return steps
            .GroupBy(step => step.RouteId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)[.. group.Select(step => step.TravelNodeId!.Value)]
            );
    }
}
