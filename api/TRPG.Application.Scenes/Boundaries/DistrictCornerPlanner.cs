using TRPG.Application.Scenes.Neighbors;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Boundaries;

internal record CornerPocket(
    Guid OverhangingId,
    Guid InsetId,
    EdgeSpan InsetGap,
    CornerStub Stub,
    BoundarySegment Closure
);

internal record CornerStub(double MinX, double MinY, double MaxX, double MaxY)
{
    public bool Covers(BoundarySegment segment) =>
        segment.Kind == BoundarySegmentKind.Wall
        && segment.Placement.X >= MinX
        && segment.Placement.X <= MaxX
        && segment.Placement.Y >= MinY
        && segment.Placement.Y <= MaxY;
}

public static class DistrictCornerPlanner
{
    private const double Epsilon = 0.01;

    public static IReadOnlyCollection<NeighborDistrict> Seal(
        Footprint size,
        IReadOnlyCollection<NeighborDistrict> neighbors
    )
    {
        var pockets = Find(size, neighbors);

        return neighbors
            .Select(neighbor =>
                SealCorners(
                    neighbor,
                    pockets.Where(pocket => pocket.OverhangingId == neighbor.LocationId).ToArray()
                )
            )
            .ToArray();
    }

    internal static IReadOnlyCollection<CornerPocket> Find(
        Footprint size,
        IReadOnlyCollection<NeighborDistrict> neighbors
    )
    {
        var placed = neighbors.Select(neighbor => (Side: SideOf(size, neighbor), neighbor));
        var sided = placed
            .Where(entry => entry.Side != null)
            .Select(entry => new SidedNeighbor(entry.Side!.Value, entry.neighbor))
            .ToArray();

        var perpendicularPairs = sided
            .SelectMany(first =>
                sided
                    .Where(second => IsHorizontal(second.Side) != IsHorizontal(first.Side))
                    .Select(second => (first, second))
            )
            .ToArray();

        return perpendicularPairs
            .Select(pair => PocketBetween(size, pair.first, pair.second))
            .OfType<CornerPocket>()
            .ToArray();
    }

    private static NeighborDistrict SealCorners(
        NeighborDistrict neighbor,
        IReadOnlyCollection<CornerPocket> pockets
    ) =>
        pockets.Count == 0
            ? neighbor
            : neighbor with
            {
                Segments =
                [
                    .. neighbor.Segments.Where(segment =>
                        !pockets.Any(pocket => pocket.Stub.Covers(segment))
                    ),
                    .. pockets.Select(pocket => pocket.Closure),
                ],
            };

    private static CornerPocket? PocketBetween(
        Footprint size,
        SidedNeighbor overhanging,
        SidedNeighbor inset
    )
    {
        var alongX = IsHorizontal(overhanging.Side);
        var cornerAlong = AtMin(inset.Side) ? 0 : Length(size, alongX);
        var wallLine = AtMin(inset.Side)
            ? Low(overhanging.District, alongX)
            : High(overhanging.District, alongX);
        var cornerAcross = AtMin(overhanging.Side) ? 0 : Length(size, !alongX);
        var insetLine = AtMin(overhanging.Side)
            ? Low(inset.District, !alongX)
            : High(inset.District, !alongX);
        var overhang = Math.Abs(wallLine - cornerAlong);
        var gap = Math.Abs(insetLine - cornerAcross);

        var overhangs = AtMin(inset.Side) ? wallLine < cornerAlong : wallLine > cornerAlong;
        var inFromCorner = AtMin(overhanging.Side)
            ? insetLine > cornerAcross
            : insetLine < cornerAcross;
        var reachesInset =
            wallLine >= Low(inset.District, alongX) - Epsilon
            && wallLine <= High(inset.District, alongX) + Epsilon;
        if (!overhangs || !inFromCorner || overhang < Epsilon || gap < Epsilon || !reachesInset)
        {
            return null;
        }

        return new CornerPocket(
            overhanging.District.LocationId,
            inset.District.LocationId,
            new EdgeSpan(Math.Min(cornerAcross, insetLine), Math.Max(cornerAcross, insetLine)),
            StubOf(alongX, cornerAlong, wallLine, cornerAcross),
            ClosureOf(alongX, wallLine, cornerAcross, insetLine)
        );
    }

    private static CornerStub StubOf(
        bool alongX,
        double cornerAlong,
        double wallLine,
        double edgeLine
    )
    {
        var low = Math.Min(cornerAlong, wallLine);
        var high = Math.Max(cornerAlong, wallLine);

        return alongX
            ? new CornerStub(low, edgeLine - Epsilon, high, edgeLine + Epsilon)
            : new CornerStub(edgeLine - Epsilon, low, edgeLine + Epsilon, high);
    }

    private static BoundarySegment ClosureOf(
        bool alongX,
        double wallLine,
        double cornerAcross,
        double insetLine
    )
    {
        var middle = (cornerAcross + insetLine) / 2;
        var length = Math.Abs(insetLine - cornerAcross);

        return alongX
            ? new BoundarySegment(
                BoundarySegmentKind.Wall,
                new Placement(wallLine, middle, 0),
                new Footprint(DistrictBoundaryPlanner.WallThickness, length)
            )
            : new BoundarySegment(
                BoundarySegmentKind.Wall,
                new Placement(middle, wallLine, 0),
                new Footprint(length, DistrictBoundaryPlanner.WallThickness)
            );
    }

    private static CompassDirection? SideOf(Footprint size, NeighborDistrict neighbor)
    {
        if (Near(neighbor.Origin.Y + neighbor.Size.Depth, 0))
        {
            return CompassDirection.North;
        }

        if (Near(neighbor.Origin.Y, size.Depth))
        {
            return CompassDirection.South;
        }

        if (Near(neighbor.Origin.X + neighbor.Size.Width, 0))
        {
            return CompassDirection.West;
        }

        return Near(neighbor.Origin.X, size.Width) ? CompassDirection.East : null;
    }

    private static bool Near(double value, double target) => Math.Abs(value - target) < Epsilon;

    private static bool IsHorizontal(CompassDirection side) =>
        side is CompassDirection.North or CompassDirection.South;

    private static bool AtMin(CompassDirection side) =>
        side is CompassDirection.North or CompassDirection.West;

    private static double Length(Footprint size, bool alongX) => alongX ? size.Width : size.Depth;

    private static double Low(NeighborDistrict neighbor, bool alongX) =>
        alongX ? neighbor.Origin.X : neighbor.Origin.Y;

    private static double High(NeighborDistrict neighbor, bool alongX) =>
        alongX ? neighbor.Origin.X + neighbor.Size.Width : neighbor.Origin.Y + neighbor.Size.Depth;

    private sealed record SidedNeighbor(CompassDirection Side, NeighborDistrict District);
}
