using TRPG.Domain.Models;

namespace TRPG.Application.Common.Navigation;

public record RouteStop(double AtMeters, Point Position);

public record RouteLeg(
    Guid OriginLocationId,
    Guid ConnectorId,
    Guid DestinationLocationId,
    Guid ArrivalNodeId,
    double Distance,
    RouteStop? Stop = null
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
    private readonly Dictionary<Guid, Point> _positionByNode = [];

    public TravelGraph(IEnumerable<Connector> connectors, IEnumerable<TravelNode> nodes)
    {
        _connectors = [.. connectors];
        _nodes = [.. nodes];

        foreach (var node in _nodes)
        {
            GetOrAdd(_nodeIdsByLocation, node.LocationId).Add(node.Id);
            _locationIdByNode[node.Id] = node.LocationId;
            _positionByNode[node.Id] = node.Position;
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
        var edges = Dijkstra(sources, goals.Contains);

        return edges is null ? [] : ToLegs(edges);
    }

    public Guid? FindNearestLocation(Guid originLocationId, IReadOnlySet<Guid> candidateLocationIds)
    {
        if (candidateLocationIds.Contains(originLocationId))
        {
            return originLocationId;
        }

        var edges = Dijkstra(
            NodesOf(originLocationId),
            node => candidateLocationIds.Contains(_locationIdByNode[node])
        );

        return edges is { Count: > 0 } ? _locationIdByNode[edges[^1].To] : null;
    }

    public IReadOnlyList<RouteLeg> BuildCycle(IReadOnlyList<Guid> orderedLocations)
    {
        var legs = new List<RouteLeg>();
        Guid? currentNodeId = null;

        for (var index = 0; index < orderedLocations.Count; index++)
        {
            var next = orderedLocations[(index + 1) % orderedLocations.Count];
            var segment = FindShortestPath(orderedLocations[index], currentNodeId, next);
            legs.AddRange(segment);
            currentNodeId = segment.Count > 0 ? segment[^1].ArrivalNodeId : currentNodeId;
        }

        return legs.Count == 0 ? legs : WithWrapDistance(legs);
    }

    public IReadOnlyList<RouteLeg> BuildNodeCycle(IReadOnlyList<Guid> stopNodeIds)
    {
        if (stopNodeIds.Count < 2)
        {
            return [];
        }

        var segments = new List<(List<GraphEdge> Edges, Guid StopNodeId)>();
        var current = stopNodeIds[0];

        foreach (var stop in stopNodeIds.Skip(1).Append(stopNodeIds[0]))
        {
            var segment = Dijkstra([current], node => node == stop);
            if (segment == null)
            {
                continue;
            }

            segments.Add((segment, stop));
            current = stop;
        }

        return ToStoppedLegs(segments);
    }

    public double ShortestDistance(Guid fromNodeId, Guid toNodeId)
    {
        if (fromNodeId == toNodeId)
        {
            return 0;
        }

        var edges = Dijkstra([fromNodeId], node => node == toNodeId, pointConnectorsOnly: true);

        return edges?.Sum(edge => edge.Distance) ?? 0;
    }

    public Guid OriginNodeOf(Guid connectorId) => _locationConnectorById[connectorId].OriginNodeId;

    public Guid ArrivalNodeOf(Guid connectorId) =>
        _locationConnectorById[connectorId].DestinationNodeId;

    private List<RouteLeg> ToStoppedLegs(List<(List<GraphEdge> Edges, Guid StopNodeId)> segments)
    {
        var legs = new List<RouteLeg>();
        var pending = 0.0;
        RouteStop? pendingStop = null;

        foreach (var (edges, stopNodeId) in segments)
        {
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
                            pending,
                            pendingStop
                        )
                    );
                    pending = 0;
                    pendingStop = null;
                }
                else
                {
                    pending += edge.Distance;
                }
            }

            pendingStop ??= new RouteStop(pending, _positionByNode[stopNodeId]);
        }

        if (legs.Count > 0)
        {
            legs[0] = WithWrapWalk(legs[0], pending, pendingStop);
        }

        return legs;
    }

    private static RouteStop? ShiftedBy(RouteStop? stop, double meters) =>
        stop == null ? null : stop with { AtMeters = stop.AtMeters + meters };

    private static RouteLeg WithWrapWalk(RouteLeg first, double wrapMeters, RouteStop? wrapStop) =>
        first with
        {
            Distance = first.Distance + wrapMeters,
            Stop = wrapStop ?? ShiftedBy(first.Stop, wrapMeters),
        };

    private List<RouteLeg> WithWrapDistance(List<RouteLeg> legs)
    {
        var first = _locationConnectorById[legs[0].ConnectorId];
        var wrap = ShortestDistance(legs[^1].ArrivalNodeId, first.OriginNodeId);
        legs[0] = legs[0] with { Distance = wrap };

        return legs;
    }

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

    private List<GraphEdge>? Dijkstra(
        IEnumerable<Guid> sources,
        Func<Guid, bool> isGoal,
        bool pointConnectorsOnly = false
    )
    {
        var costs = new Dictionary<Guid, double>();
        var cameFrom = new Dictionary<Guid, GraphEdge>();
        var frontier = new PriorityQueue<Guid, double>();

        foreach (var source in sources)
        {
            costs[source] = 0;
            frontier.Enqueue(source, 0);
        }

        while (frontier.TryDequeue(out var node, out var cost))
        {
            if (cost > costs[node])
            {
                continue;
            }

            if (isGoal(node))
            {
                return Reconstruct(node, cameFrom);
            }

            foreach (var edge in _edgesByOriginNode.GetValueOrDefault(node, []))
            {
                if (pointConnectorsOnly && edge.Connector is not PointConnector)
                {
                    continue;
                }

                var next = cost + edge.Distance;
                if (!costs.TryGetValue(edge.To, out var known) || next < known)
                {
                    costs[edge.To] = next;
                    cameFrom[edge.To] = edge with { From = node };
                    frontier.Enqueue(edge.To, next);
                }
            }
        }

        return null;
    }

    private static List<GraphEdge> Reconstruct(Guid goal, Dictionary<Guid, GraphEdge> cameFrom)
    {
        var edges = new List<GraphEdge>();
        var node = goal;

        while (cameFrom.TryGetValue(node, out var edge))
        {
            edges.Add(edge);
            node = edge.From;
        }

        edges.Reverse();

        return edges;
    }

    private static List<RouteLeg> ToLegs(List<GraphEdge> edges)
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
