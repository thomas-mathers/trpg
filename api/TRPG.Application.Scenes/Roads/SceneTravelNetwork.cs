using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Roads;

public record SceneTravelNode(Guid Id, Point Position, bool IsPort);

public record SceneTravelEdge(IReadOnlyList<Point> Points, double Distance, bool Bidirectional);

public record SceneTravelNetwork(
    IReadOnlyCollection<SceneTravelNode> Nodes,
    IReadOnlyCollection<SceneTravelEdge> Edges
);

internal static class PointNetworkMapper
{
    internal static SceneTravelNetwork ToTravelNetwork(LocationPointNetwork network)
    {
        var nodeById = network.Nodes.ToDictionary(node => node.Id);

        return new SceneTravelNetwork(
            [
                .. network.Nodes.Select(node => new SceneTravelNode(
                    node.Id,
                    node.Position,
                    network.PortNodeIds.Contains(node.Id)
                )),
            ],
            [
                .. network.Connectors.Select(connector => new SceneTravelEdge(
                    [
                        nodeById[connector.OriginNodeId].Position,
                        .. connector.Waypoints.Points,
                        nodeById[connector.DestinationNodeId].Position,
                    ],
                    connector.Distance,
                    connector.Bidirectional
                )),
            ]
        );
    }
}
