using TRPG.Application.Scenes.Boundaries;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Scenes.Roads;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Neighbors;

public record NeighborSource(
    Guid LocationId,
    Footprint Size,
    Placement ReverseExit,
    IReadOnlyCollection<BoundaryExit> Exits,
    IReadOnlyCollection<SceneNearbyBuildingInfo> Buildings,
    IReadOnlyCollection<ScenePropInfo> Props,
    IReadOnlyCollection<SceneGreenSpaceInfo> GreenSpaces,
    IReadOnlyCollection<DistrictRoad> Roads
);

public record NeighborDistrict(
    Guid LocationId,
    Placement Origin,
    Footprint Size,
    IReadOnlyCollection<SceneNearbyBuildingInfo> Buildings,
    IReadOnlyCollection<ScenePropInfo> Props,
    IReadOnlyCollection<SceneGreenSpaceInfo> GreenSpaces,
    IReadOnlyCollection<BoundarySegment> Segments,
    IReadOnlyCollection<DistrictRoad> Roads
);

public static class NeighborPreviewPlanner
{
    private const double CornerSliverTolerance = 3;

    public static NeighborDistrict? Plan(Footprint size, Placement exit, NeighborSource source)
    {
        var edge = DistrictBoundaryPlanner.EdgeOf(size, exit);
        if (edge == null)
        {
            return null;
        }

        var origin = OriginOf(size, exit, edge.Value, source);
        var sharedSpan = SharedSpan(size, edge.Value, origin, source.Size);
        var segments = WallSegments(source, OffsetBy(sharedSpan, edge.Value, origin));

        return new NeighborDistrict(
            source.LocationId,
            origin,
            source.Size,
            source.Buildings.Select(building => Translate(building, origin)).ToArray(),
            source
                .Props.Select(prop => prop with { Placement = Shift(prop.Placement, origin) })
                .ToArray(),
            source
                .GreenSpaces.Select(space =>
                    space with
                    {
                        Placement = Shift(space.Placement, origin),
                    }
                )
                .ToArray(),
            segments.Select(segment => Translate(segment, origin)).ToArray(),
            source.Roads.Select(road => Translate(road, origin)).ToArray()
        );
    }

    public static bool Overlaps(NeighborDistrict first, NeighborDistrict second) =>
        Penetration(first.Origin.X, first.Size.Width, second.Origin.X, second.Size.Width)
            > CornerSliverTolerance
        && Penetration(first.Origin.Y, first.Size.Depth, second.Origin.Y, second.Size.Depth)
            > CornerSliverTolerance;

    private static double Penetration(
        double firstStart,
        double firstLength,
        double secondStart,
        double secondLength
    ) =>
        Math.Min(firstStart + firstLength, secondStart + secondLength)
        - Math.Max(firstStart, secondStart);

    public static EdgeSpan SharedSpan(
        Footprint size,
        CompassDirection edge,
        Placement origin,
        Footprint neighborSize
    )
    {
        var horizontal = edge is CompassDirection.North or CompassDirection.South;
        var offset = horizontal ? origin.X : origin.Y;
        var ownLength = horizontal ? size.Width : size.Depth;
        var neighborLength = horizontal ? neighborSize.Width : neighborSize.Depth;

        return new EdgeSpan(Math.Max(0, offset), Math.Min(ownLength, offset + neighborLength));
    }

    private static EdgeSpan OffsetBy(EdgeSpan span, CompassDirection edge, Placement origin)
    {
        var offset = edge is CompassDirection.North or CompassDirection.South ? origin.X : origin.Y;

        return new EdgeSpan(span.Start - offset, span.End - offset);
    }

    private static IReadOnlyCollection<BoundarySegment> WallSegments(
        NeighborSource source,
        EdgeSpan sharedSpan
    )
    {
        var sharedEdge = DistrictBoundaryPlanner.EdgeOf(source.Size, source.ReverseExit);
        var exits = source
            .Exits.Select(exit =>
                DistrictBoundaryPlanner.EdgeOf(source.Size, exit.Placement) == sharedEdge
                    ? exit with
                    {
                        Opening = sharedSpan,
                    }
                    : exit with
                    {
                        Kind = BoundaryExitKind.Wilderness,
                    }
            )
            .ToArray();
        var boundary = DistrictBoundaryPlanner.Plan(source.Size, exits);

        return boundary
            .Segments.Concat(boundary.Gates.Select(gate => GateInfill(source.Size, gate)))
            .ToArray();
    }

    private static BoundarySegment GateInfill(Footprint size, BoundaryGate gate)
    {
        var edge = DistrictBoundaryPlanner.EdgeOf(size, gate.Placement);
        var horizontal = edge is CompassDirection.North or CompassDirection.South;

        return new BoundarySegment(
            BoundarySegmentKind.Wall,
            gate.Placement with
            {
                Angle = 0,
            },
            horizontal
                ? new Footprint(gate.Width, DistrictBoundaryPlanner.WallThickness)
                : new Footprint(DistrictBoundaryPlanner.WallThickness, gate.Width)
        );
    }

    private static Placement OriginOf(
        Footprint size,
        Placement exit,
        CompassDirection edge,
        NeighborSource source
    )
    {
        var reverse = source.ReverseExit;

        return edge switch
        {
            CompassDirection.North => new Placement(
                exit.X - reverse.X,
                -source.Size.Depth,
                Angle: 0
            ),
            CompassDirection.South => new Placement(exit.X - reverse.X, size.Depth, Angle: 0),
            CompassDirection.West => new Placement(
                -source.Size.Width,
                exit.Y - reverse.Y,
                Angle: 0
            ),
            _ => new Placement(size.Width, exit.Y - reverse.Y, Angle: 0),
        };
    }

    private static SceneNearbyBuildingInfo Translate(
        SceneNearbyBuildingInfo building,
        Placement origin
    ) => building with { Placement = Shift(building.Placement, origin) };

    private static BoundarySegment Translate(BoundarySegment segment, Placement origin) =>
        segment with
        {
            Placement = Shift(segment.Placement, origin),
        };

    private static DistrictRoad Translate(DistrictRoad road, Placement origin) =>
        road with
        {
            Points = road
                .Points.Select(point => new Point(point.X + origin.X, point.Y + origin.Y))
                .ToArray(),
        };

    private static Placement Shift(Placement placement, Placement origin) =>
        placement with
        {
            X = placement.X + origin.X,
            Y = placement.Y + origin.Y,
        };
}
