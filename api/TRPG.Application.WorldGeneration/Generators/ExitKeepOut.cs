using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class ExitKeepOut
{
    private const double DoorSize = 1.5;

    internal static OrientedBox Of(ConnectorExit exit) =>
        Of(exit.Point, exit.FacingAngle, exit.Stairs);

    internal static OrientedBox Of(PlanarPoint point, double facingAngle, StairDirection? stairs)
    {
        var width = stairs is null ? DoorSize : StairPlan.Width;
        var depth = stairs is null ? DoorSize : StairPlan.Depth;
        var (sin, cos) = Math.SinCos(facingAngle);

        return new OrientedBox(
            point.X + depth / 2 * sin,
            point.Y - depth / 2 * cos,
            Math.Abs(cos) * width + Math.Abs(sin) * depth,
            Math.Abs(sin) * width + Math.Abs(cos) * depth,
            0
        );
    }
}
