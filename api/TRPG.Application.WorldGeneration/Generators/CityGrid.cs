using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class CityGrid
{
    internal const double CellSize = CityLattice.CellSize;
    internal const double HalfCell = CellSize / 2;
    internal const double GateCorridor = 5 * CellSize;

    internal static double SnapUp(double value) => Math.Ceiling(value / CellSize - 1e-9) * CellSize;

    internal static double SnapDown(double value) => Math.Floor(value / CellSize + 1e-9) * CellSize;

    internal static double SnapUpToOddCells(double value)
    {
        var cells = (int)Math.Ceiling(value / CellSize - 1e-9);

        return (cells % 2 == 0 ? cells + 1 : cells) * CellSize;
    }

    internal static int CellOf(double value) => (int)Math.Floor(value / CellSize);

    internal static double CentreOf(int cell) => (cell + 0.5) * CellSize;

    internal static OrientedBox CellBox(Placement placement, Footprint footprint)
    {
        var extent = CellExtent(placement, footprint);

        return new OrientedBox(
            SnapCentre(placement.X, extent.Width),
            SnapCentre(placement.Y, extent.Depth),
            extent.Width,
            extent.Depth,
            Angle: 0
        );
    }

    internal static Placement SnapCentre(Placement placement, Footprint footprint)
    {
        var box = CellBox(placement, footprint);

        return placement with
        {
            X = box.CenterX,
            Y = box.CenterY,
        };
    }

    private static Footprint CellExtent(Placement placement, Footprint footprint)
    {
        var quarterTurned = Math.Abs(Math.Sin(placement.Angle)) > 0.5;
        var width = quarterTurned ? footprint.Depth : footprint.Width;
        var depth = quarterTurned ? footprint.Width : footprint.Depth;

        return new Footprint(CellsFor(width) * CellSize, CellsFor(depth) * CellSize);
    }

    private static int CellsFor(double length) =>
        Math.Max(1, (int)Math.Ceiling(length / CellSize - 1e-9));

    private static double SnapCentre(double centre, double extent) =>
        Math.Floor((centre - extent / 2) / CellSize + 0.5) * CellSize + extent / 2;

    internal static Footprint SnapBuilding(Footprint footprint) =>
        new(Width: SnapUpToOddCells(footprint.Width), Depth: SnapUp(footprint.Depth));
}
