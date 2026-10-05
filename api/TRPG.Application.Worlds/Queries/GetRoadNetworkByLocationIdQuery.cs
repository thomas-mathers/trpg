using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetRoadNetworkByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

public record LocationRoadNetwork(
    IReadOnlyCollection<RoadNode> Nodes,
    IReadOnlyCollection<RoadEdge> Edges
);

internal class GetRoadNetworkByLocationIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetRoadNetworkByLocationIdQuery, LocationRoadNetwork>
{
    public async Task<LocationRoadNetwork> Handle(
        GetRoadNetworkByLocationIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var nodes = await context
            .RoadNodes.AsNoTracking()
            .Where(node => node.LocationId == query.LocationId)
            .ToArrayAsync(cancellationToken);
        var edges = await context
            .RoadEdges.AsNoTracking()
            .Where(edge => edge.LocationId == query.LocationId)
            .ToArrayAsync(cancellationToken);

        return new LocationRoadNetwork(nodes, edges);
    }
}
