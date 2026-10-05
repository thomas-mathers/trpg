using TRPG.Application.Scenes.Boundaries;
using TRPG.Application.Scenes.Results;

namespace TRPG.Application.Scenes.Neighbors;

public static class NeighborWallTrimmer
{
    private const double Tolerance = DistrictBoundaryPlanner.WallThickness / 2 + 0.01;
    private const double MinimumLength = 0.01;

    public static IReadOnlyCollection<NeighborDistrict> Trim(
        IReadOnlyCollection<NeighborDistrict> neighbors
    ) =>
        neighbors
            .Select(neighbor =>
                neighbor with
                {
                    Segments = neighbor
                        .Segments.SelectMany(segment => Clip(segment, neighbor, neighbors))
                        .ToArray(),
                }
            )
            .ToArray();

    public static IReadOnlyCollection<NeighborDistrict> TrimAround(
        IReadOnlyCollection<NeighborDistrict> neighbors,
        IReadOnlyCollection<SceneNearbyBuildingInfo> buildings
    ) =>
        neighbors
            .Select(neighbor =>
                neighbor with
                {
                    Segments = neighbor
                        .Segments.SelectMany(segment => ClipAround(segment, buildings))
                        .ToArray(),
                }
            )
            .ToArray();

    private static IEnumerable<BoundarySegment> ClipAround(
        BoundarySegment segment,
        IEnumerable<SceneNearbyBuildingInfo> buildings
    )
    {
        var horizontal = segment.Footprint.Width >= segment.Footprint.Depth;
        var along = AlongRange(segment, horizontal);
        var cuts = buildings
            .Select(building => CutByBuilding(segment, building, horizontal, along))
            .OfType<(double Start, double End)>()
            .OrderBy(cut => cut.Start)
            .ToArray();

        return Split(segment, horizontal, along, cuts);
    }

    private static (double Start, double End)? CutByBuilding(
        BoundarySegment segment,
        SceneNearbyBuildingInfo building,
        bool horizontal,
        (double Start, double End) along
    )
    {
        var sideways = Math.Abs(Math.Sin(building.Placement.Angle)) > 0.5;
        var width = sideways ? building.Footprint.Depth : building.Footprint.Width;
        var depth = sideways ? building.Footprint.Width : building.Footprint.Depth;
        var crossCentre = horizontal ? segment.Placement.Y : segment.Placement.X;
        var crossHalf = (horizontal ? depth : width) / 2;
        var buildingCross = horizontal ? building.Placement.Y : building.Placement.X;
        if (Math.Abs(crossCentre - buildingCross) >= crossHalf)
        {
            return null;
        }

        var alongHalf = (horizontal ? width : depth) / 2;
        var buildingAlong = horizontal ? building.Placement.X : building.Placement.Y;
        var start = Math.Max(along.Start, buildingAlong - alongHalf);
        var end = Math.Min(along.End, buildingAlong + alongHalf);

        return end - start > MinimumLength ? (start, end) : null;
    }

    private static IEnumerable<BoundarySegment> Clip(
        BoundarySegment segment,
        NeighborDistrict owner,
        IEnumerable<NeighborDistrict> neighbors
    )
    {
        var horizontal = segment.Footprint.Width >= segment.Footprint.Depth;
        var along = AlongRange(segment, horizontal);
        var cuts = neighbors
            .Where(other => other.LocationId != owner.LocationId)
            .Select(other => CutBy(segment, other, horizontal, along))
            .OfType<(double Start, double End)>()
            .OrderBy(cut => cut.Start)
            .ToArray();

        return Split(segment, horizontal, along, cuts);
    }

    private static IEnumerable<BoundarySegment> Split(
        BoundarySegment segment,
        bool horizontal,
        (double Start, double End) along,
        (double Start, double End)[] cuts
    ) =>
        cuts.Length == 0
            ? [segment]
            : Remainders(along, cuts)
                .Select(piece => Piece(segment, piece.Start, piece.End, horizontal));

    private static (double Start, double End) AlongRange(BoundarySegment segment, bool horizontal)
    {
        var centre = horizontal ? segment.Placement.X : segment.Placement.Y;
        var half = (horizontal ? segment.Footprint.Width : segment.Footprint.Depth) / 2;

        return (centre - half, centre + half);
    }

    private static (double Start, double End)? CutBy(
        BoundarySegment segment,
        NeighborDistrict other,
        bool horizontal,
        (double Start, double End) along
    )
    {
        var crossCentre = horizontal ? segment.Placement.Y : segment.Placement.X;
        var crossHalf = (horizontal ? segment.Footprint.Depth : segment.Footprint.Width) / 2;
        var crossStart = horizontal ? other.Origin.Y : other.Origin.X;
        var crossEnd = crossStart + (horizontal ? other.Size.Depth : other.Size.Width);
        if (
            crossCentre - crossHalf < crossStart - Tolerance
            || crossCentre + crossHalf > crossEnd + Tolerance
        )
        {
            return null;
        }

        var alongStart = horizontal ? other.Origin.X : other.Origin.Y;
        var alongEnd = alongStart + (horizontal ? other.Size.Width : other.Size.Depth);
        var start = Math.Max(along.Start, alongStart);
        var end = Math.Min(along.End, alongEnd);

        return end - start > MinimumLength ? (start, end) : null;
    }

    private static IEnumerable<(double Start, double End)> Remainders(
        (double Start, double End) along,
        IEnumerable<(double Start, double End)> cuts
    )
    {
        var cursor = along.Start;
        foreach (var cut in cuts)
        {
            if (cut.Start - cursor > MinimumLength)
            {
                yield return (cursor, cut.Start);
            }

            cursor = Math.Max(cursor, cut.End);
        }

        if (along.End - cursor > MinimumLength)
        {
            yield return (cursor, along.End);
        }
    }

    private static BoundarySegment Piece(
        BoundarySegment segment,
        double start,
        double end,
        bool horizontal
    )
    {
        var centre = (start + end) / 2;
        var length = end - start;

        return horizontal
            ? segment with
            {
                Placement = segment.Placement with { X = centre },
                Footprint = segment.Footprint with { Width = length },
            }
            : segment with
            {
                Placement = segment.Placement with { Y = centre },
                Footprint = segment.Footprint with { Depth = length },
            };
    }
}
