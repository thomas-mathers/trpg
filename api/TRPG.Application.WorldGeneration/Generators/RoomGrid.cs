using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class RoomGrid
{
    internal const double CellSize = 0.5;

    private const double Tolerance = 1e-9;

    internal static double SnapUp(double value) =>
        Math.Ceiling(value / CellSize - Tolerance) * CellSize;

    internal static double SnapDown(double value) =>
        Math.Floor(value / CellSize + Tolerance) * CellSize;

    internal static double SnapNearest(double value) =>
        Math.Round(value / CellSize, MidpointRounding.AwayFromZero) * CellSize;

    internal static double CellArea(Footprint footprint) =>
        SnapUp(footprint.Width) * SnapUp(footprint.Depth);

    internal static OrientedBox CellBox(Placement pose, Footprint footprint)
    {
        var corners = OrientedBox.From(pose, footprint).Corners();
        var left = SnapDown(corners.Min(corner => corner.X));
        var top = SnapDown(corners.Min(corner => corner.Y));
        var right = SnapUp(corners.Max(corner => corner.X));
        var bottom = SnapUp(corners.Max(corner => corner.Y));

        return new OrientedBox(
            (left + right) / 2,
            (top + bottom) / 2,
            right - left,
            bottom - top,
            0
        );
    }

    internal static IEnumerable<Placement> Alignments(Placement pose, Footprint footprint)
    {
        var corners = OrientedBox.From(pose, footprint).Corners();
        var shiftsX = Shifts(corners.Min(corner => corner.X));
        var shiftsY = Shifts(corners.Min(corner => corner.Y));

        return shiftsX
            .SelectMany(shiftX => shiftsY.Select(shiftY => new PlanarPoint(shiftX, shiftY)))
            .OrderBy(shift => Math.Abs(shift.X) + Math.Abs(shift.Y))
            .Select(shift => pose with { X = pose.X + shift.X, Y = pose.Y + shift.Y });
    }

    private static double[] Shifts(double edge)
    {
        var down = SnapDown(edge) - edge;
        var up = SnapUp(edge) - edge;

        if (Math.Abs(down) < Tolerance || Math.Abs(up) < Tolerance)
        {
            return [0];
        }

        return [down, up];
    }
}
