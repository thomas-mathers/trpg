using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetPointNetworkByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

public record LocationPointNetwork(
    IReadOnlyCollection<TravelNode> Nodes,
    IReadOnlyCollection<PointConnector> Connectors,
    IReadOnlySet<Guid> PortNodeIds
);

internal class GetPointNetworkByLocationIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetPointNetworkByLocationIdQuery, LocationPointNetwork>
{
    public async Task<LocationPointNetwork> Handle(
        GetPointNetworkByLocationIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var nodes = await context
            .TravelNodes.AsNoTracking()
            .Where(node => node.LocationId == query.LocationId)
            .ToArrayAsync(cancellationToken);
        var connectors = await context
            .PointConnectors.AsNoTracking()
            .Where(connector => connector.LocationId == query.LocationId)
            .ToArrayAsync(cancellationToken);

        var portNodeIds = await context
            .LocationConnectors.AsNoTracking()
            .Where(connector =>
                connector.OriginLocationId == query.LocationId
                || connector.DestinationLocationId == query.LocationId
            )
            .Select(connector => new { connector.OriginNodeId, connector.DestinationNodeId })
            .ToArrayAsync(cancellationToken);
        var nodeIds = nodes.Select(node => node.Id).ToHashSet();

        return new LocationPointNetwork(
            nodes,
            connectors,
            portNodeIds
                .SelectMany(ids => new[] { ids.OriginNodeId, ids.DestinationNodeId })
                .Where(nodeIds.Contains)
                .ToHashSet()
        );
    }
}
