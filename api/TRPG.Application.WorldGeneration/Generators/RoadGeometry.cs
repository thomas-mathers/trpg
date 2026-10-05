using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class RoadGeometry
{
    private const double Epsilon = 1e-9;

    internal static double Distance(Point a, Point b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    internal static double Length(IReadOnlyList<Point> points) =>
        points.Zip(points.Skip(1), Distance).Sum();

    internal static Point Project(Point point, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;

        if (lengthSquared == 0)
        {
            return start;
        }

        var along = (point.X - start.X) * dx + (point.Y - start.Y) * dy;
        var t = Math.Clamp(along / lengthSquared, 0, 1);

        return new Point(start.X + dx * t, start.Y + dy * t);
    }

    internal static List<Point> Simplify(IReadOnlyList<Point> points)
    {
        var simplified = new List<Point>();

        foreach (var point in points)
        {
            if (simplified.Count > 0 && Distance(simplified[^1], point) < Epsilon)
            {
                continue;
            }

            while (simplified.Count >= 2 && IsCollinear(simplified[^2], simplified[^1], point))
            {
                simplified.RemoveAt(simplified.Count - 1);
            }

            simplified.Add(point);
        }

        return simplified;
    }

    internal static bool IsCollinear(Point a, Point b, Point c) =>
        Math.Abs((b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X)) < Epsilon;

    internal static List<Point> Dedupe(IReadOnlyList<Point> points, double tolerance)
    {
        var kept = new List<Point> { points[0] };

        foreach (var point in points.Skip(1).Take(points.Count - 2))
        {
            if (Distance(kept[^1], point) >= tolerance)
            {
                kept.Add(point);
            }
        }

        kept.Add(points[^1]);

        return kept;
    }
}
