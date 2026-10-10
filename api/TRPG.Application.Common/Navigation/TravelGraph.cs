using TRPG.Application.Common.Algorithms;
using TRPG.Domain.Models;

namespace TRPG.Application.Common.Navigation;

public record RouteLeg(
    Guid OriginLocationId,
    Guid ConnectorId,
    Guid DestinationLocationId,
    Guid ArrivalNodeId,
    double Distance
);

public record DirectedTravelLeg(
    Guid FromNodeId,
    Guid ToNodeId,
    Guid ConnectorId,
    double Distance,
    Polyline Path
);

public sealed class TravelGraph
{
    private readonly Connector[] _connectors;
    private readonly TravelNode[] _nodes;
    private readonly Dictionary<Guid, List<GraphEdge>> _edgesByOriginNode = [];
    private readonly Dictionary<Guid, LocationConnector> _locationConnectorById = [];
    private readonly Dictionary<Guid, List<LocationConnector>> _connectorsByOriginLocation = [];
    private readonly Dictionary<Guid, List<Guid>> _nodeIdsByLocation = [];
    private readonly Dictionary<Guid, Guid> _locationIdByNode = [];

    public TravelGraph(IEnumerable<Connector> connectors, IEnumerable<TravelNode> nodes)
    {
        _connectors = [.. connectors];
        _nodes = [.. nodes];

        foreach (var node in _nodes)
        {
            GetOrAdd(_nodeIdsByLocation, node.LocationId).Add(node.Id);
            _locationIdByNode[node.Id] = node.LocationId;
        }

        foreach (var connector in _connectors)
        {
            AddEdges(connector);
        }
    }

    public TravelGraph WhereLocation(Func<Guid, bool> includeLocation) =>
        new(
            _connectors.Where(connector =>
                connector switch
                {
                    LocationConnector location => includeLocation(location.OriginLocationId)
                        && includeLocation(location.DestinationLocationId),
                    PointConnector point => includeLocation(point.LocationId),
                    _ => false,
                }
            ),
            _nodes.Where(node => includeLocation(node.LocationId))
        );

    public IReadOnlyList<LocationConnector> ConnectorsFrom(Guid locationId) =>
        _connectorsByOriginLocation.GetValueOrDefault(locationId, []);

    public IReadOnlyList<RouteLeg> FindShortestPath(
        Guid originLocationId,
        Guid? startNodeId,
        Guid destinationLocationId
    )
    {
        if (originLocationId == destinationLocationId)
        {
            return [];
        }

        IReadOnlyCollection<Guid> sources = startNodeId is { } start
            ? [start]
            : NodesOf(originLocationId);
        var goals = NodesOf(destinationLocationId).ToHashSet();
        var edges = ShortestEdges(sources, goals, pointConnectorsOnly: false);

        return ToLegs(edges);
    }

    public IReadOnlyList<DirectedTravelLeg> FindShortestPath(Guid fromNodeId, Guid toNodeId)
    {
        var edges = ShortestEdges(
            [fromNodeId],
            new HashSet<Guid> { toNodeId },
            pointConnectorsOnly: false
        );

        return ToDirectedLegs(edges);
    }

    public DirectedTravelLeg SnapshotLeg(Guid fromNodeId, Guid toNodeId, Guid connectorId)
    {
        var edge = _edgesByOriginNode
            .GetValueOrDefault(fromNodeId, [])
            .SingleOrDefault(edge => edge.To == toNodeId && edge.Connector.Id == connectorId);
        if (edge.Connector is null)
        {
            throw new InvalidOperationException("The circuit leg does not match the travel graph.");
        }

        return ToDirectedLegs([edge with { From = fromNodeId }]).Single();
    }

    public Guid? FindNearestLocation(Guid originLocationId, IReadOnlySet<Guid> candidateLocationIds)
    {
        if (candidateLocationIds.Contains(originLocationId))
        {
            return originLocationId;
        }

        var nodes = ShortestNodes(
            NodesOf(originLocationId),
            node => candidateLocationIds.Contains(_locationIdByNode[node]),
            pointConnectorsOnly: false
        );

        return nodes.Count > 0 ? _locationIdByNode[nodes[^1]] : null;
    }

    public double ShortestDistance(Guid fromNodeId, Guid toNodeId)
    {
        if (fromNodeId == toNodeId)
        {
            return 0;
        }

        var edges = ShortestEdges(
            [fromNodeId],
            new HashSet<Guid> { toNodeId },
            pointConnectorsOnly: true
        );

        return edges.Sum(edge => edge.Distance);
    }

    public Guid OriginNodeOf(Guid connectorId) => _locationConnectorById[connectorId].OriginNodeId;

    public Guid ArrivalNodeOf(Guid connectorId) =>
        _locationConnectorById[connectorId].DestinationNodeId;

    public Guid LocationOf(Guid nodeId) => _locationIdByNode[nodeId];

    private IReadOnlyCollection<Guid> NodesOf(Guid locationId) =>
        _nodeIdsByLocation.GetValueOrDefault(locationId, []);

    private void AddEdges(Connector connector)
    {
        if (connector is LocationConnector location)
        {
            _locationConnectorById[location.Id] = location;
            GetOrAdd(_connectorsByOriginLocation, location.OriginLocationId).Add(location);
        }

        GetOrAdd(_edgesByOriginNode, connector.OriginNodeId)
            .Add(new GraphEdge(connector, connector.DestinationNodeId));

        if (connector is PointConnector { Bidirectional: true })
        {
            GetOrAdd(_edgesByOriginNode, connector.DestinationNodeId)
                .Add(new GraphEdge(connector, connector.OriginNodeId));
        }
    }

    private IReadOnlyList<GraphEdge> ShortestEdges(
        IReadOnlyCollection<Guid> sources,
        IReadOnlySet<Guid> destinations,
        bool pointConnectorsOnly = false
    )
    {
        var nodes = ShortestNodes(sources, destinations.Contains, pointConnectorsOnly);

        return nodes
            .Zip(nodes.Skip(1))
            .Select(pair => EdgeBetween(pair.First, pair.Second, pointConnectorsOnly))
            .ToArray();
    }

    private IReadOnlyList<Guid> ShortestNodes(
        IReadOnlyCollection<Guid> sources,
        Func<Guid, bool> isDestination,
        bool pointConnectorsOnly
    ) =>
        Graphs.ShortestPathToNearest(
            sources,
            isDestination,
            node => EdgesFrom(node, pointConnectorsOnly).Select(edge => edge.To).Distinct(),
            (from, to) => EdgeBetween(from, to, pointConnectorsOnly).Distance
        );

    private IEnumerable<GraphEdge> EdgesFrom(Guid nodeId, bool pointConnectorsOnly) =>
        _edgesByOriginNode
            .GetValueOrDefault(nodeId, [])
            .Where(edge => !pointConnectorsOnly || edge.Connector is PointConnector);

    private GraphEdge EdgeBetween(Guid fromNodeId, Guid toNodeId, bool pointConnectorsOnly) =>
        EdgesFrom(fromNodeId, pointConnectorsOnly)
            .Where(edge => edge.To == toNodeId)
            .MinBy(edge => edge.Distance) with
        {
            From = fromNodeId,
        };

    private static List<RouteLeg> ToLegs(IReadOnlyList<GraphEdge> edges)
    {
        var legs = new List<RouteLeg>();
        var pending = 0.0;

        foreach (var edge in edges)
        {
            if (edge.Connector is LocationConnector location)
            {
                legs.Add(
                    new RouteLeg(
                        location.OriginLocationId,
                        location.Id,
                        location.DestinationLocationId,
                        location.DestinationNodeId,
                        pending
                    )
                );
                pending = 0;
            }
            else
            {
                pending += edge.Distance;
            }
        }

        return legs;
    }

    private IReadOnlyList<DirectedTravelLeg> ToDirectedLegs(IReadOnlyList<GraphEdge> edges) =>
        edges
            .Select(edge => new DirectedTravelLeg(
                edge.From,
                edge.To,
                edge.Connector.Id,
                edge.Distance,
                PathFor(edge)
            ))
            .ToArray();

    private Polyline PathFor(GraphEdge edge)
    {
        var points = edge.Connector switch
        {
            PointConnector point => PointPath(point, edge),
            LocationConnector location when location.Path is not null => location.Path.Points,
            LocationConnector => [_nodes.Single(node => node.Id == edge.To).Position],
            _ => [],
        };

        return new Polyline { Points = points.ToList() };
    }

    private IReadOnlyList<Point> PointPath(PointConnector connector, GraphEdge edge)
    {
        var points = new List<Point>
        {
            _nodes.Single(node => node.Id == connector.OriginNodeId).Position,
        };
        points.AddRange(connector.Waypoints.Points);
        points.Add(_nodes.Single(node => node.Id == connector.DestinationNodeId).Position);

        return edge.From == connector.OriginNodeId
            ? points
            : points.AsEnumerable().Reverse().ToArray();
    }

    private static List<T> GetOrAdd<T>(Dictionary<Guid, List<T>> map, Guid key)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        return list;
    }

    private readonly record struct GraphEdge(Connector Connector, Guid To)
    {
        public Guid From { get; init; }

        public double Distance => Connector.Distance;
    }
}
