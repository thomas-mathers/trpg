using TRPG.Application.Common.Algorithms;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing;

internal record RoutePathLeg(Guid OriginLocationId, Guid ConnectorId, Guid DestinationLocationId);

internal static class RoutePathfinder
{
    public static IReadOnlyList<RoutePathLeg> FindShortestPath(
        IReadOnlyCollection<LocationConnector> connectors,
        IReadOnlyCollection<TravelConnector> travelConnectors,
        Guid originLocationId,
        Guid destinationLocationId
    )
    {
        var distanceByConnectorId = travelConnectors.ToDictionary(
            connector => connector.ConnectorId,
            connector => connector.Distance
        );
        var edges = connectors
            .Where(connector => distanceByConnectorId.ContainsKey(connector.Id))
            .Select(connector => new RoutePathEdge(
                connector.OriginLocationId,
                connector.Id,
                connector.DestinationLocationId,
                distanceByConnectorId[connector.Id]
            ))
            .ToArray();
        if (
            edges
                .GroupBy(edge => (edge.OriginLocationId, edge.DestinationLocationId))
                .Any(group => group.Count() > 1)
        )
        {
            throw new InvalidOperationException(
                "Multiple measured travel connectors join the same ordered pair of locations."
            );
        }

        var edgesByEndpoints = edges.ToDictionary(edge =>
            (edge.OriginLocationId, edge.DestinationLocationId)
        );
        var graph = edges
            .GroupBy(edge => edge.OriginLocationId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(edge => edge.DestinationLocationId).ToArray()
            );
        var locations = Graphs.ShortestPath(
            originLocationId,
            destinationLocationId,
            locationId => graph.GetValueOrDefault(locationId, []),
            (origin, destination) => edgesByEndpoints[(origin, destination)].Distance
        );
        if (locations.Count < 2)
        {
            return [];
        }

        var path = new List<RoutePathLeg>();
        for (var index = 0; index < locations.Count - 1; index++)
        {
            var origin = locations[index];
            var destination = locations[index + 1];
            var edge = edgesByEndpoints[(origin, destination)];
            path.Add(new RoutePathLeg(origin, edge.ConnectorId, destination));
        }
        return path.ToArray();
    }

    private record RoutePathEdge(
        Guid OriginLocationId,
        Guid ConnectorId,
        Guid DestinationLocationId,
        float Distance
    );
}
