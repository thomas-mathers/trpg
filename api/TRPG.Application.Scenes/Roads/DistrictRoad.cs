using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Roads;

public record DistrictRoad(IReadOnlyList<Point> Points, double Width, RoadClass Class);

internal static class SceneRoadMapper
{
    internal static IReadOnlyCollection<DistrictRoad> ToRoads(LocationRoadNetwork network)
    {
        var nodeById = network.Nodes.ToDictionary(node => node.Id);

        return network
            .Edges.Select(edge => new DistrictRoad(
                [
                    Position(nodeById[edge.FromNodeId]),
                    .. edge.Waypoints.Points,
                    Position(nodeById[edge.ToNodeId]),
                ],
                RoadClassWidths.Of(edge.Class),
                edge.Class
            ))
            .ToArray();
    }

    private static Point Position(RoadNode node) => new(node.X, node.Y);
}
