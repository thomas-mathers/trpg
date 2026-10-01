using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal readonly record struct PlanarPoint(double X, double Y);

internal sealed record OrientedBox(
    double CenterX,
    double CenterY,
    double Width,
    double Depth,
    double Angle
)
{
    private const double Tolerance = 1e-9;

    public static OrientedBox From(Placement placement, Footprint footprint) =>
        new(placement.X, placement.Y, footprint.Width, footprint.Depth, placement.Angle);

    public OrientedBox Inflated(double margin) =>
        this with
        {
            Width = Width + (2 * margin),
            Depth = Depth + (2 * margin),
        };

    public IReadOnlyList<PlanarPoint> Corners()
    {
        var halfWidth = Width / 2;
        var halfDepth = Depth / 2;
        return
        [
            ToWorld(-halfWidth, -halfDepth),
            ToWorld(halfWidth, -halfDepth),
            ToWorld(halfWidth, halfDepth),
            ToWorld(-halfWidth, halfDepth),
        ];
    }

    public bool IsInside(double boundsWidth, double boundsDepth) =>
        Corners()
            .All(corner =>
                corner.X >= -Tolerance
                && corner.X <= boundsWidth + Tolerance
                && corner.Y >= -Tolerance
                && corner.Y <= boundsDepth + Tolerance
            );

    public bool Overlaps(OrientedBox other)
    {
        var ownCorners = Corners();
        var otherCorners = other.Corners();
        return Axes()
            .Concat(other.Axes())
            .All(axis => ProjectionsOverlap(axis, ownCorners, otherCorners));
    }

    private PlanarPoint ToWorld(double localX, double localY)
    {
        var (sin, cos) = Math.SinCos(Angle);
        return new PlanarPoint(
            CenterX + (localX * cos) - (localY * sin),
            CenterY + (localX * sin) + (localY * cos)
        );
    }

    private PlanarPoint[] Axes()
    {
        var (sin, cos) = Math.SinCos(Angle);
        return [new PlanarPoint(cos, sin), new PlanarPoint(-sin, cos)];
    }

    private static bool ProjectionsOverlap(
        PlanarPoint axis,
        IReadOnlyList<PlanarPoint> first,
        IReadOnlyList<PlanarPoint> second
    )
    {
        var firstProjection = first.Select(point => Project(point, axis)).ToArray();
        var secondProjection = second.Select(point => Project(point, axis)).ToArray();
        return firstProjection.Max() - secondProjection.Min() > Tolerance
            && secondProjection.Max() - firstProjection.Min() > Tolerance;
    }

    private static double Project(PlanarPoint point, PlanarPoint axis) =>
        (point.X * axis.X) + (point.Y * axis.Y);
}
