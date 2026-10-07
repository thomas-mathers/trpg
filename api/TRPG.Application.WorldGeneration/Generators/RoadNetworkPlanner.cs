using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record RoadNetwork(
    IReadOnlyList<TravelNode> Nodes,
    IReadOnlyList<PointConnector> Edges,
    IReadOnlyDictionary<Guid, Guid> PortNodeIdByConnectorId
);

internal static class RoadNetworkPlanner
{
    internal static RoadNetwork Plan(
        Location district,
        IReadOnlyCollection<RoadBuilding> buildings,
        IReadOnlyCollection<RoadTerminal> terminals
    )
    {
        var size = new Footprint(Width: district.Width, Depth: district.Depth);
        var graph = new RoadGraphBuilder();

        foreach (var road in DistrictRoadPlanner.Plan(size, buildings, terminals))
        {
            graph.Add(road);
        }

        var classes = RoadClassifier.Classify(graph.Nodes, graph.Paths);

        return new RoadNetwork(
            graph.Nodes.Select(node => ToNode(district, node)).ToArray(),
            graph.Paths.Select(path => ToEdge(district, path, classes[path])).ToArray(),
            graph
                .Nodes.Where(node => node.Terminal is not null)
                .ToDictionary(node => node.Terminal!.ConnectorId, node => node.Id)
        );
    }

    private static TravelNode ToNode(Location district, RoadGraphNode node) =>
        new()
        {
            Id = node.Id,
            WorldId = district.WorldId,
            LocationId = district.Id,
            Position = node.Position,
        };

    private static PointConnector ToEdge(
        Location district,
        RoadGraphPath path,
        RoadClass roadClass
    ) =>
        new()
        {
            WorldId = district.WorldId,
            LocationId = district.Id,
            OriginNodeId = path.From.Id,
            DestinationNodeId = path.To.Id,
            Bidirectional = true,
            RoadClass = roadClass,
            Distance = RoadGeometry.Length(path.Points),
            Waypoints = new Polyline
            {
                Points = path.Points.Skip(1).Take(path.Points.Count - 2).ToList(),
            },
        };
}
