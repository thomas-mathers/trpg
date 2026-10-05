using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class DistrictExitSnapper
{
    private const double Epsilon = 1e-6;

    private record EdgeKey(bool Horizontal, double Fixed);

    internal static IReadOnlyList<ConnectorExit> Snap(
        Footprint frame,
        IReadOnlyCollection<ConnectorExit> exits
    )
    {
        var onEdge = exits.Where(exit => KeyOf(frame, exit) is not null).ToArray();
        var snapped = onEdge
            .GroupBy(exit => KeyOf(frame, exit)!)
            .SelectMany(group => SnapEdge(frame, group.Key, group.ToArray()));

        return [.. exits.Except(onEdge), .. snapped];
    }

    private static EdgeKey? KeyOf(Footprint frame, ConnectorExit exit)
    {
        var point = exit.Point;

        if (point.Y < Epsilon || point.Y > frame.Depth - Epsilon)
        {
            return new EdgeKey(Horizontal: true, point.Y);
        }

        return point.X < Epsilon || point.X > frame.Width - Epsilon
            ? new EdgeKey(Horizontal: false, point.X)
            : null;
    }

    private static IEnumerable<ConnectorExit> SnapEdge(
        Footprint frame,
        EdgeKey key,
        ConnectorExit[] exits
    )
    {
        var length = key.Horizontal ? frame.Width : frame.Depth;
        var ordered = exits.OrderBy(exit => AlongEdge(key, exit)).ToArray();
        var positions = SnapPositions(ordered.Select(exit => AlongEdge(key, exit)), length);

        return ordered.Select(
            (exit, index) =>
                exit with
                {
                    Point = key.Horizontal
                        ? new PlanarPoint(positions[index], exit.Point.Y)
                        : new PlanarPoint(exit.Point.X, positions[index]),
                }
        );
    }

    private static double AlongEdge(EdgeKey key, ConnectorExit exit) =>
        key.Horizontal ? exit.Point.X : exit.Point.Y;

    private static double[] SnapPositions(IEnumerable<double> sorted, double length)
    {
        var first = CityGrid.CentreOf(0);
        var last = CityGrid.CentreOf(CityGrid.CellOf(length - Epsilon));
        var positions = sorted
            .Select(position =>
                Math.Clamp(CityGrid.CentreOf(CityGrid.CellOf(position)), first, last)
            )
            .ToArray();

        for (var index = 1; index < positions.Length; index++)
        {
            positions[index] = Math.Max(
                positions[index],
                positions[index - 1] + ConnectorPointResolver.MinimumBearingSeparation
            );
        }

        var overflow = positions.Length > 0 ? positions[^1] - last : 0;
        var shift =
            overflow > 0 ? Math.Ceiling(overflow / CityGrid.CellSize) * CityGrid.CellSize : 0;

        return positions.Select(position => Math.Max(first, position - shift)).ToArray();
    }
}
