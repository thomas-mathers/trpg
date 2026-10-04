namespace TRPG.Application.WorldGeneration.Generators;

internal readonly record struct PlanRect(double X, double Y, double Width, double Depth)
{
    private const double Tolerance = 1e-6;

    internal double Right => X + Width;

    internal double Bottom => Y + Depth;

    internal double CenterX => X + Width / 2;

    internal double CenterY => Y + Depth / 2;

    internal bool Contains(PlanarPoint point) =>
        point.X >= X - Tolerance
        && point.X <= Right + Tolerance
        && point.Y >= Y - Tolerance
        && point.Y <= Bottom + Tolerance;

    internal OrientedBox ToBox() => new(CenterX, CenterY, Width, Depth, 0);
}
