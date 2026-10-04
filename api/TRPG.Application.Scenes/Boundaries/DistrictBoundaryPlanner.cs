using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Boundaries;

public enum BoundaryExitKind
{
    District,
    Wilderness,
}

public enum BoundarySegmentKind
{
    Wall,
    Tower,
}

public record BoundaryExit(Guid ConnectorId, BoundaryExitKind Kind, Placement Placement);

public record BoundarySegment(BoundarySegmentKind Kind, Placement Placement, Footprint Footprint);

public record BoundaryGate(Guid ConnectorId, Placement Placement, double Width);

public record DistrictBoundary(
    IReadOnlyCollection<BoundarySegment> Segments,
    IReadOnlyCollection<BoundaryGate> Gates,
    IReadOnlyCollection<CompassDirection> OpenEdges
);

public static class DistrictBoundaryPlanner
{
    public const double WallThickness = 0.4;
    public const double GateWidth = 4;
    public const double TowerSize = 1.6;

    private const double EdgeSnap = 1;
    private const double MinimumWallSpan = 0.1;
    private const double GateReach = GateWidth / 2 + TowerSize;

    public static DistrictBoundary Plan(Footprint size, IReadOnlyCollection<BoundaryExit> exits)
    {
        var segments = new List<BoundarySegment>();
        var gates = new List<BoundaryGate>();
        var openEdges = new List<CompassDirection>();

        foreach (var edge in EdgesOf(size))
        {
            var onEdge = exits.Where(exit => ClosestEdge(size, exit) == edge.Side).ToArray();

            if (onEdge.Any(exit => exit.Kind == BoundaryExitKind.District))
            {
                openEdges.Add(edge.Side);
                continue;
            }

            var gateExits = onEdge.OrderBy(exit => edge.Along(exit.Placement)).ToArray();
            gates.AddRange(gateExits.Select(exit => ToGate(edge, exit)));
            segments.AddRange(WallEdge(edge, gateExits.Select(exit => Centre(edge, exit))));
        }

        return new DistrictBoundary(segments, gates, openEdges);
    }

    private static IEnumerable<Edge> EdgesOf(Footprint size) =>
        [
            new(CompassDirection.North, Horizontal: true, Line: 0, Length: size.Width),
            new(CompassDirection.East, Horizontal: false, Line: size.Width, Length: size.Depth),
            new(CompassDirection.South, Horizontal: true, Line: size.Depth, Length: size.Width),
            new(CompassDirection.West, Horizontal: false, Line: 0, Length: size.Depth),
        ];

    private static CompassDirection? ClosestEdge(Footprint size, BoundaryExit exit)
    {
        var closest = EdgesOf(size)
            .Select(edge => new { edge.Side, Gap = edge.DistanceTo(exit.Placement) })
            .Where(candidate => candidate.Gap <= EdgeSnap)
            .OrderBy(candidate => candidate.Gap)
            .FirstOrDefault();

        return closest?.Side;
    }

    private static double Centre(Edge edge, BoundaryExit exit) =>
        edge.Length < 2 * GateReach
            ? edge.Length / 2
            : Math.Clamp(edge.Along(exit.Placement), GateReach, edge.Length - GateReach);

    private static BoundaryGate ToGate(Edge edge, BoundaryExit exit)
    {
        var onWall = edge.PlacementAt(Centre(edge, exit));

        return new BoundaryGate(
            exit.ConnectorId,
            onWall with
            {
                Angle = exit.Placement.Angle,
            },
            GateWidth
        );
    }

    private static IEnumerable<BoundarySegment> WallEdge(Edge edge, IEnumerable<double> gateCentres)
    {
        var segments = new List<BoundarySegment>();
        var cursor = 0.0;

        foreach (var centre in gateCentres)
        {
            segments.AddRange(Span(edge, cursor, centre - GateReach));
            segments.AddRange(Towers(edge, centre));
            cursor = Math.Max(cursor, centre + GateReach);
        }

        segments.AddRange(Span(edge, cursor, edge.Length));

        return segments;
    }

    private static IEnumerable<BoundarySegment> Span(Edge edge, double start, double end)
    {
        if (end - start < MinimumWallSpan)
        {
            return [];
        }

        var middle = (start + end) / 2;
        var span = end - start;
        var footprint = edge.Horizontal
            ? new Footprint(span, WallThickness)
            : new Footprint(WallThickness, span);

        return [new BoundarySegment(BoundarySegmentKind.Wall, edge.PlacementAt(middle), footprint)];
    }

    private static IEnumerable<BoundarySegment> Towers(Edge edge, double centre)
    {
        var offset = GateWidth / 2 + TowerSize / 2;

        return
        [
            new BoundarySegment(
                BoundarySegmentKind.Tower,
                edge.PlacementAt(centre - offset),
                new Footprint(TowerSize, TowerSize)
            ),
            new BoundarySegment(
                BoundarySegmentKind.Tower,
                edge.PlacementAt(centre + offset),
                new Footprint(TowerSize, TowerSize)
            ),
        ];
    }

    private sealed record Edge(CompassDirection Side, bool Horizontal, double Line, double Length)
    {
        public double Along(Placement placement) => Horizontal ? placement.X : placement.Y;

        public double DistanceTo(Placement placement) =>
            Math.Abs((Horizontal ? placement.Y : placement.X) - Line);

        public Placement PlacementAt(double along) =>
            Horizontal ? new Placement(along, Line, 0) : new Placement(Line, along, 0);
    }
}
