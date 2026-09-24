using TRPG.Application.Common.Algorithms;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record TravelGraphEdge(Guid ConnectorId, Guid DestinationLocationId, float Distance);

internal record TravelPathLeg(
    Guid OriginLocationId,
    Guid ConnectorId,
    Guid DestinationLocationId,
    float Distance
);

internal static class TravelGraph
{
    public static IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> Build(
        WorldGeneratorResult world
    ) => Build(world.LocationConnectors, world.TravelConnectors);

    public static IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> Build(
        IReadOnlyCollection<LocationConnector> locationConnectors,
        IReadOnlyCollection<TravelConnector> travelConnectors
    )
    {
        var travelConnectorsByConnectorId = travelConnectors.ToDictionary(connector =>
            connector.ConnectorId
        );
        return locationConnectors
            .Where(connector => travelConnectorsByConnectorId.ContainsKey(connector.Id))
            .GroupBy(connector => connector.OriginLocationId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var edges = group
                        .Select(connector => new TravelGraphEdge(
                            connector.Id,
                            connector.DestinationLocationId,
                            travelConnectorsByConnectorId[connector.Id].Distance
                        ))
                        .ToArray();

                    if (
                        edges
                            .GroupBy(edge => edge.DestinationLocationId)
                            .Any(group => group.Count() > 1)
                    )
                    {
                        throw new InvalidOperationException(
                            $"Multiple travel connectors originate at {group.Key} and lead to the same destination."
                        );
                    }

                    return (IReadOnlyList<TravelGraphEdge>)edges;
                }
            );
    }

    public static IReadOnlyList<TravelPathLeg> FindShortestPath(
        IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> graph,
        Guid originLocationId,
        Guid destinationLocationId
    )
    {
        var edgesByEndpoints = graph
            .SelectMany(pair => pair.Value.Select(edge => (pair.Key, Edge: edge)))
            .ToDictionary(
                pair => (OriginLocationId: pair.Key, pair.Edge.DestinationLocationId),
                pair => pair.Edge
            );

        var locations = Graphs.ShortestPath(
            originLocationId,
            destinationLocationId,
            locationId =>
                graph.GetValueOrDefault(locationId, []).Select(edge => edge.DestinationLocationId),
            (from, to) => edgesByEndpoints[(from, to)].Distance
        );

        if (locations.Count < 2)
        {
            return [];
        }

        var legs = new List<TravelPathLeg>();
        for (var index = 0; index < locations.Count - 1; index++)
        {
            var origin = locations[index];
            var destination = locations[index + 1];
            var edge = edgesByEndpoints[(origin, destination)];
            legs.Add(new TravelPathLeg(origin, edge.ConnectorId, destination, edge.Distance));
        }

        return legs.ToArray();
    }

    public static IReadOnlyList<TravelPathLeg> BuildCycle(
        IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> graph,
        IReadOnlyList<Guid> orderedLocations
    )
    {
        var legs = new List<TravelPathLeg>();
        for (var index = 0; index < orderedLocations.Count; index++)
        {
            var from = orderedLocations[index];
            var to = orderedLocations[(index + 1) % orderedLocations.Count];
            legs.AddRange(FindShortestPath(graph, from, to));
        }
        return legs.ToArray();
    }
}
