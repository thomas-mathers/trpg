using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Routing.Queries;

public class GetRouteIdsByLocationIdQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class GetRouteIdsByLocationIdQueryHandler(IRoutingDbContext context)
    : IQueryHandler<GetRouteIdsByLocationIdQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetRouteIdsByLocationIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .RouteSteps.AsNoTracking()
            .Where(step => step.WorldId == query.WorldId && step.LocationId == query.LocationId)
            .Select(step => step.RouteId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
}
