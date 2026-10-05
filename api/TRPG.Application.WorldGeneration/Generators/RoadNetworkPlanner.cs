using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record RoadNetwork(IReadOnlyList<RoadNode> Nodes, IReadOnlyList<RoadEdge> Edges);

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
            graph.Paths.Select(path => ToEdge(district, path, classes[path])).ToArray()
        );
    }

    private static RoadNode ToNode(Location district, RoadGraphNode node) =>
        new()
        {
            Id = node.Id,
            WorldId = district.WorldId,
            LocationId = district.Id,
            Kind = node.Terminal is null ? RoadNodeKind.Junction : RoadNodeKind.Port,
            ConnectorId = node.Terminal?.ConnectorId,
            X = node.Position.X,
            Y = node.Position.Y,
        };

    private static RoadEdge ToEdge(Location district, RoadGraphPath path, RoadClass roadClass) =>
        new()
        {
            WorldId = district.WorldId,
            LocationId = district.Id,
            FromNodeId = path.From.Id,
            ToNodeId = path.To.Id,
            Class = roadClass,
            Length = RoadGeometry.Length(path.Points),
            Waypoints = new Polyline
            {
                Points = path.Points.Skip(1).Take(path.Points.Count - 2).ToList(),
            },
        };
}
