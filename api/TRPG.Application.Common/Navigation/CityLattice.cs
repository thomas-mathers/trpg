using TRPG.Domain.Models;

namespace TRPG.Application.Common.Navigation;

public static class CityLattice
{
    public const double CellSize = 1.5;

    private const double BuildingClearance = 2.25;

    public static bool IsWithinBuildingClearance(
        Placement placement,
        Footprint footprint,
        Point point
    )
    {
        var (sin, cos) = Math.SinCos(placement.Angle);
        var dx = point.X - placement.X;
        var dy = point.Y - placement.Y;
        var local = new Point(dx * cos + dy * sin, -dx * sin + dy * cos);

        return Math.Abs(local.X) < footprint.Width / 2 + BuildingClearance - 1e-6
            && Math.Abs(local.Y) < footprint.Depth / 2 + BuildingClearance - 1e-6;
    }
}
