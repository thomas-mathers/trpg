using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal enum ConnectorExitKind
{
    Compass,
    SouthDoor,
    NorthStairs,
    NorthEastStairs,
    NorthWestStairs,
    SideDoor,
    Bearing,
    Fixed,
}

internal record ConnectorExitRequest(
    Guid ConnectorId,
    Guid DestinationLocationId,
    ConnectorExitKind Kind
)
{
    internal CompassDirection Direction { get; init; }
    internal int SideIndex { get; init; }
    internal double BearingRadians { get; init; }
    internal PlanarPoint FixedPoint { get; init; }
    internal double FixedFacingAngle { get; init; }
}

internal record ConnectorExit(Guid ConnectorId, PlanarPoint Point, double FacingAngle);

internal static class ConnectorPointResolver
{
    internal const double ArrivalInset = 1;
    internal const double MinimumBearingSeparation = 6;
    internal const double SideDoorSpacing = 1.5;
    internal const double CornerInset = 0.5;
    private const double EdgeEndInset = 1;

    internal static IReadOnlyList<ConnectorExit> ResolveExits(
        Footprint frame,
        IReadOnlyCollection<ConnectorExitRequest> requests
    )
    {
        var exits = new List<ConnectorExit>();
        var evenlySpaced = requests.Where(IsEvenlySpacedOnEdge).GroupBy(request => EdgeOf(request));

        foreach (var group in evenlySpaced)
        {
            exits.AddRange(
                SpreadEvenly(
                    frame,
                    group.Key,
                    group.OrderBy(r => r.DestinationLocationId).ToArray()
                )
            );
        }

        foreach (
            var group in requests
                .Where(request => request.Kind == ConnectorExitKind.Bearing)
                .GroupBy(request => BearingEdge(frame, request.BearingRadians))
        )
        {
            exits.AddRange(SpreadByBearing(frame, group.Key, group.ToArray()));
        }

        exits.AddRange(
            requests
                .Where(request => IsDirectlyPlaced(request))
                .Select(request => PlaceDirectly(frame, request))
        );

        return exits;
    }

    internal static Placement ResolveArrival(ConnectorExit reverseExit) =>
        new(
            X: reverseExit.Point.X + ArrivalInset * Math.Sin(reverseExit.FacingAngle),
            Y: reverseExit.Point.Y - ArrivalInset * Math.Cos(reverseExit.FacingAngle),
            Angle: reverseExit.FacingAngle
        );

    private static bool IsEvenlySpacedOnEdge(ConnectorExitRequest request) =>
        request.Kind is ConnectorExitKind.SouthDoor or ConnectorExitKind.NorthStairs
        || (request.Kind == ConnectorExitKind.Compass && IsCardinal(request.Direction));

    private static bool IsDirectlyPlaced(ConnectorExitRequest request) =>
        request.Kind
            is ConnectorExitKind.NorthEastStairs
                or ConnectorExitKind.NorthWestStairs
                or ConnectorExitKind.SideDoor
                or ConnectorExitKind.Fixed
        || (request.Kind == ConnectorExitKind.Compass && !IsCardinal(request.Direction));

    private static bool IsCardinal(CompassDirection direction) =>
        direction
            is CompassDirection.North
                or CompassDirection.East
                or CompassDirection.South
                or CompassDirection.West;

    private static FrameEdge EdgeOf(ConnectorExitRequest request) =>
        request.Kind switch
        {
            ConnectorExitKind.SouthDoor => FrameEdge.South,
            ConnectorExitKind.NorthStairs => FrameEdge.North,
            _ => request.Direction switch
            {
                CompassDirection.North => FrameEdge.North,
                CompassDirection.East => FrameEdge.East,
                CompassDirection.South => FrameEdge.South,
                _ => FrameEdge.West,
            },
        };

    private static IEnumerable<ConnectorExit> SpreadEvenly(
        Footprint frame,
        FrameEdge edge,
        IReadOnlyList<ConnectorExitRequest> requests
    )
    {
        var length = EdgeLength(frame, edge);

        return requests.Select(
            (request, index) =>
                new ConnectorExit(
                    request.ConnectorId,
                    PointOnEdge(frame, edge, length * (index + 1) / (requests.Count + 1)),
                    FacingInward(edge)
                )
        );
    }

    private static FrameEdge BearingEdge(Footprint frame, double bearing) =>
        BearingHit(frame, bearing).Edge;

    private static BearingHitPoint BearingHit(Footprint frame, double bearing)
    {
        var directionX = Math.Sin(bearing);
        var directionY = -Math.Cos(bearing);
        var distanceToSide =
            Math.Abs(directionX) > 1e-9 ? frame.Width / 2 / Math.Abs(directionX) : double.MaxValue;
        var distanceToEnd =
            Math.Abs(directionY) > 1e-9 ? frame.Depth / 2 / Math.Abs(directionY) : double.MaxValue;

        if (distanceToSide < distanceToEnd)
        {
            var edge = directionX > 0 ? FrameEdge.East : FrameEdge.West;
            return new BearingHitPoint(edge, frame.Depth / 2 + directionY * distanceToSide);
        }

        var endEdge = directionY < 0 ? FrameEdge.North : FrameEdge.South;
        return new BearingHitPoint(endEdge, frame.Width / 2 + directionX * distanceToEnd);
    }

    private static IEnumerable<ConnectorExit> SpreadByBearing(
        Footprint frame,
        FrameEdge edge,
        IReadOnlyList<ConnectorExitRequest> requests
    )
    {
        var length = EdgeLength(frame, edge);
        var ordered = requests
            .Select(request => new
            {
                Request = request,
                Position = BearingHit(frame, request.BearingRadians).Position,
            })
            .OrderBy(entry => entry.Position)
            .ThenBy(entry => entry.Request.DestinationLocationId)
            .ToArray();
        var positions = SeparatePositions(
            ordered.Select(entry => entry.Position).ToArray(),
            length
        );

        return ordered.Select(
            (entry, index) =>
                new ConnectorExit(
                    entry.Request.ConnectorId,
                    PointOnEdge(frame, edge, positions[index]),
                    FacingInward(edge)
                )
        );
    }

    private static double[] SeparatePositions(double[] sortedPositions, double length)
    {
        var upper = length - EdgeEndInset;
        var positions = sortedPositions
            .Select(position => Math.Clamp(position, EdgeEndInset, upper))
            .ToArray();

        for (var index = 1; index < positions.Length; index++)
        {
            positions[index] = Math.Max(
                positions[index],
                positions[index - 1] + MinimumBearingSeparation
            );
        }

        var overflow = positions.Length > 0 ? positions[^1] - upper : 0;

        return overflow > 0
            ? positions.Select(position => Math.Max(EdgeEndInset, position - overflow)).ToArray()
            : positions;
    }

    private static ConnectorExit PlaceDirectly(Footprint frame, ConnectorExitRequest request) =>
        request.Kind switch
        {
            ConnectorExitKind.Fixed => new ConnectorExit(
                request.ConnectorId,
                request.FixedPoint,
                request.FixedFacingAngle
            ),
            ConnectorExitKind.SideDoor => SideDoor(frame, request),
            ConnectorExitKind.NorthEastStairs => Corner(frame, request, FrameCorner.NorthEast),
            ConnectorExitKind.NorthWestStairs => Corner(frame, request, FrameCorner.NorthWest),
            _ => Corner(frame, request, CornerOf(request.Direction)),
        };

    private static ConnectorExit SideDoor(Footprint frame, ConnectorExitRequest request)
    {
        var edge = request.SideIndex % 2 == 0 ? FrameEdge.East : FrameEdge.West;
        var position = SideDoorSpacing + SideDoorSpacing * (request.SideIndex / 2);

        return new ConnectorExit(
            request.ConnectorId,
            PointOnEdge(frame, edge, Math.Min(position, frame.Depth - EdgeEndInset)),
            FacingInward(edge)
        );
    }

    private static FrameCorner CornerOf(CompassDirection direction) =>
        direction switch
        {
            CompassDirection.Northeast => FrameCorner.NorthEast,
            CompassDirection.Southeast => FrameCorner.SouthEast,
            CompassDirection.Southwest => FrameCorner.SouthWest,
            _ => FrameCorner.NorthWest,
        };

    private static ConnectorExit Corner(
        Footprint frame,
        ConnectorExitRequest request,
        FrameCorner corner
    )
    {
        var east = corner is FrameCorner.NorthEast or FrameCorner.SouthEast;
        var south = corner is FrameCorner.SouthEast or FrameCorner.SouthWest;
        var point = new PlanarPoint(
            east ? frame.Width - CornerInset : CornerInset,
            south ? frame.Depth - CornerInset : CornerInset
        );

        return new ConnectorExit(request.ConnectorId, point, FacingInward(corner));
    }

    private static double FacingInward(FrameCorner corner) =>
        corner switch
        {
            FrameCorner.NorthEast => 5 * Math.PI / 4,
            FrameCorner.SouthEast => 7 * Math.PI / 4,
            FrameCorner.SouthWest => Math.PI / 4,
            _ => 3 * Math.PI / 4,
        };

    private static double EdgeLength(Footprint frame, FrameEdge edge) =>
        edge is FrameEdge.North or FrameEdge.South ? frame.Width : frame.Depth;

    private static PlanarPoint PointOnEdge(Footprint frame, FrameEdge edge, double position) =>
        edge switch
        {
            FrameEdge.North => new PlanarPoint(position, 0),
            FrameEdge.South => new PlanarPoint(position, frame.Depth),
            FrameEdge.West => new PlanarPoint(0, position),
            _ => new PlanarPoint(frame.Width, position),
        };

    private static double FacingInward(FrameEdge edge) =>
        edge switch
        {
            FrameEdge.North => Math.PI,
            FrameEdge.East => 3 * Math.PI / 2,
            FrameEdge.South => 0,
            _ => Math.PI / 2,
        };

    private enum FrameEdge
    {
        North,
        East,
        South,
        West,
    }

    private enum FrameCorner
    {
        NorthEast,
        SouthEast,
        SouthWest,
        NorthWest,
    }

    private record BearingHitPoint(FrameEdge Edge, double Position);
}
