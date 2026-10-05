using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal sealed class RoadGraphNode(Point position, RoadTerminal? terminal)
{
    internal Guid Id { get; } = Guid.NewGuid();
    internal Point Position { get; } = position;
    internal RoadTerminal? Terminal { get; } = terminal;
}

internal sealed class RoadGraphPath(RoadGraphNode from, RoadGraphNode to, List<Point> points)
{
    internal RoadGraphNode From { get; } = from;
    internal RoadGraphNode To { get; } = to;
    internal List<Point> Points { get; } = points;
}

internal sealed class RoadGraphBuilder
{
    private const double NodeTolerance = 0.05;

    private readonly List<RoadGraphNode> _nodes = [];
    private readonly List<RoadGraphPath> _paths = [];

    internal IReadOnlyList<RoadGraphNode> Nodes => _nodes;
    internal IReadOnlyList<RoadGraphPath> Paths => _paths;

    internal void Add(PlannedRoad road)
    {
        if (road.Points.Count < 2)
        {
            return;
        }

        var port = NewNode(road.Points[0], road.Terminal);
        var end = _paths.Count == 0 ? NewNode(road.Points[^1], null) : JoinAt(road.Points[^1]);

        if (end == port)
        {
            return;
        }

        var points = road.Points.Take(road.Points.Count - 1).Append(end.Position).ToList();
        _paths.Add(new RoadGraphPath(port, end, RoadGeometry.Dedupe(points, NodeTolerance)));
    }

    private RoadGraphNode NewNode(Point position, RoadTerminal? terminal)
    {
        var node = new RoadGraphNode(position, terminal);
        _nodes.Add(node);

        return node;
    }

    private RoadGraphNode JoinAt(Point point)
    {
        var existing = NodeNear(point);

        if (existing is not null)
        {
            return existing;
        }

        var hit = NearestSegment(point);
        var position =
            RoadGeometry.Distance(point, hit.Projection) < NodeTolerance ? point : hit.Projection;

        return NodeNear(position) ?? Split(hit, position);
    }

    private RoadGraphNode? NodeNear(Point point) =>
        _nodes.FirstOrDefault(node => RoadGeometry.Distance(node.Position, point) < NodeTolerance);

    private SegmentHit NearestSegment(Point point)
    {
        SegmentHit? best = null;
        var bestDistance = double.MaxValue;

        foreach (var path in _paths)
        {
            for (var index = 0; index < path.Points.Count - 1; index++)
            {
                var projection = RoadGeometry.Project(
                    point,
                    path.Points[index],
                    path.Points[index + 1]
                );
                var distance = RoadGeometry.Distance(point, projection);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = new SegmentHit(path, index, projection);
                }
            }
        }

        return best!;
    }

    private RoadGraphNode Split(SegmentHit hit, Point position)
    {
        var junction = NewNode(position, null);
        var left = hit.Path.Points.Take(hit.Index + 1).Append(position).ToList();
        var right = new[] { position }.Concat(hit.Path.Points.Skip(hit.Index + 1)).ToList();

        _paths.Remove(hit.Path);
        _paths.Add(
            new RoadGraphPath(hit.Path.From, junction, RoadGeometry.Dedupe(left, NodeTolerance))
        );
        _paths.Add(
            new RoadGraphPath(junction, hit.Path.To, RoadGeometry.Dedupe(right, NodeTolerance))
        );

        return junction;
    }

    private record SegmentHit(RoadGraphPath Path, int Index, Point Projection);
}
