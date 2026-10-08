using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class PatrolStopNodeAssigner
{
    internal static void Assign(
        IEnumerable<RouteStep> steps,
        IReadOnlyList<TravelNode> nodes,
        IReadOnlyList<PointConnector> pointConnectors,
        IReadOnlyDictionary<Guid, Location> locationsById
    )
    {
        var roadDegreeByNodeId = RoadDegrees(pointConnectors);
        var nodesByLocationId = nodes.ToLookup(node => node.LocationId);
        var stopByLocationId = new Dictionary<Guid, Guid?>();

        foreach (var step in steps)
        {
            if (!stopByLocationId.TryGetValue(step.LocationId, out var stop))
            {
                stop = PickStop(
                    nodesByLocationId[step.LocationId].ToArray(),
                    roadDegreeByNodeId,
                    locationsById[step.LocationId]
                );
                stopByLocationId[step.LocationId] = stop;
            }

            step.TravelNodeId = stop;
        }
    }

    private static Dictionary<Guid, int> RoadDegrees(IEnumerable<PointConnector> pointConnectors)
    {
        var degrees = new Dictionary<Guid, int>();
        foreach (var road in pointConnectors.Where(connector => connector.Bidirectional))
        {
            degrees[road.OriginNodeId] = degrees.GetValueOrDefault(road.OriginNodeId) + 1;
            degrees[road.DestinationNodeId] = degrees.GetValueOrDefault(road.DestinationNodeId) + 1;
        }

        return degrees;
    }

    private static Guid? PickStop(
        TravelNode[] locationNodes,
        IReadOnlyDictionary<Guid, int> roadDegreeByNodeId,
        Location location
    )
    {
        var junctions = locationNodes
            .Where(node => roadDegreeByNodeId.GetValueOrDefault(node.Id) >= 2)
            .ToArray();
        var candidates = junctions.Length > 0 ? junctions : locationNodes;

        return candidates
            .OrderBy(node => DistanceToCenter(node, location))
            .Select(node => (Guid?)node.Id)
            .FirstOrDefault();
    }

    private static double DistanceToCenter(TravelNode node, Location location) =>
        Math.Sqrt(
            Math.Pow(node.Position.X - location.Width / 2, 2)
                + Math.Pow(node.Position.Y - location.Depth / 2, 2)
        );
}
