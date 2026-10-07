using TRPG.Application.Common.Algorithms;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Navigation;

internal static class DistrictRoutePlanner
{
    public static IReadOnlyList<Point> Plan(
        Point entry,
        Point anchor,
        LocationRoadNetwork network,
        NavigationGrid offRoad
    )
    {
        var polylines = Polylines(network);
        var ports = network
            .Nodes.Where(node => node.Kind == RoadNodeKind.Port)
            .Select(node => new Point(node.X, node.Y))
            .ToArray();
        var landing = NearestLanding(anchor, polylines);

        if (ports.Length == 0 || landing == null)
        {
            return offRoad.FindPath(entry, anchor);
        }

        polylines[landing.Polyline].Insert(landing.Segment + 1, landing.Point);
        var start = ports.MinBy(port => DistanceBetween(port, entry))!;
        var road = RoadPath(start, landing.Point, polylines);

        if (road.Count == 0)
        {
            return offRoad.FindPath(entry, anchor);
        }

        return WithoutRepeats([entry, .. road, .. offRoad.FindPath(landing.Point, anchor)]);
    }

    private static List<List<Point>> Polylines(LocationRoadNetwork network)
    {
        var nodes = network.Nodes.ToDictionary(node => node.Id);

        return
        [
            .. network
                .Edges.Where(edge =>
                    nodes.ContainsKey(edge.FromNodeId) && nodes.ContainsKey(edge.ToNodeId)
                )
                .Select(edge =>
                {
                    var from = nodes[edge.FromNodeId];
                    var to = nodes[edge.ToNodeId];

                    List<Point> line =
                    [
                        new(from.X, from.Y),
                        .. edge.Waypoints.Points,
                        new(to.X, to.Y),
                    ];

                    return line;
                }),
        ];
    }

    private static Landing? NearestLanding(Point anchor, List<List<Point>> polylines)
    {
        Landing? best = null;
        var bestDistance = double.MaxValue;

        for (var line = 0; line < polylines.Count; line++)
        {
            for (var segment = 0; segment < polylines[line].Count - 1; segment++)
            {
                var point = Project(anchor, polylines[line][segment], polylines[line][segment + 1]);
                var distance = DistanceBetween(point, anchor);

                if (distance < bestDistance)
                {
                    best = new Landing(point, line, segment);
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    private static IReadOnlyList<Point> RoadPath(
        Point start,
        Point goal,
        List<List<Point>> polylines
    )
    {
        var graph = new Dictionary<Point, List<Point>>();

        foreach (var line in polylines)
        {
            foreach (
                var (from, to) in line.Zip(line.Skip(1)).Where(pair => pair.First != pair.Second)
            )
            {
                Link(graph, from, to);
                Link(graph, to, from);
            }
        }

        return Graphs.ShortestPath(
            start,
            goal,
            point => graph.GetValueOrDefault(point) ?? [],
            DistanceBetween
        );
    }

    private static void Link(Dictionary<Point, List<Point>> graph, Point from, Point to)
    {
        if (!graph.TryGetValue(from, out var neighbors))
        {
            neighbors = [];
            graph[from] = neighbors;
        }

        neighbors.Add(to);
    }

    private static Point Project(Point point, Point from, Point to)
    {
        var deltaX = to.X - from.X;
        var deltaY = to.Y - from.Y;
        var lengthSquared = deltaX * deltaX + deltaY * deltaY;

        if (lengthSquared == 0)
        {
            return from;
        }

        var fraction = Math.Clamp(
            ((point.X - from.X) * deltaX + (point.Y - from.Y) * deltaY) / lengthSquared,
            0,
            1
        );

        return new Point(from.X + deltaX * fraction, from.Y + deltaY * fraction);
    }

    private static IReadOnlyList<Point> WithoutRepeats(IEnumerable<Point> points)
    {
        var result = new List<Point>();

        foreach (var point in points)
        {
            if (result.Count == 0 || result[^1] != point)
            {
                result.Add(point);
            }
        }

        return result;
    }

    private static double DistanceBetween(Point from, Point to) =>
        Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));

    private sealed record Landing(Point Point, int Polyline, int Segment);
}
