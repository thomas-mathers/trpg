using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Roads;

public record DistrictRoad(IReadOnlyList<Point> Points, double Width, RoadClass Class);

internal static class SceneRoadMapper
{
    internal static IReadOnlyCollection<DistrictRoad> ToRoads(LocationPointNetwork network)
    {
        var nodeById = network.Nodes.ToDictionary(node => node.Id);

        return
        [
            .. network
                .Connectors.Where(connector => connector.RoadClass is not null)
                .Select(connector => new DistrictRoad(
                    [
                        nodeById[connector.OriginNodeId].Position,
                        .. connector.Waypoints.Points,
                        nodeById[connector.DestinationNodeId].Position,
                    ],
                    RoadClassWidths.Of(connector.RoadClass!.Value),
                    connector.RoadClass.Value
                )),
        ];
    }
}
