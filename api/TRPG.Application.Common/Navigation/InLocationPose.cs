using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Common.Navigation;

public static class InLocationPose
{
    public static Placement Resolve(
        IReadOnlyList<Point> path,
        GameInstant enteredAt,
        double metersPerSecond,
        GameInstant now,
        double settledAngle
    )
    {
        if (path.Count == 0)
        {
            throw new ArgumentException("A path needs at least one point.", nameof(path));
        }

        if (metersPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(metersPerSecond));
        }

        var remaining = Math.Max(0, (now - enteredAt).TotalSeconds) * metersPerSecond;

        for (var index = 1; index < path.Count; index++)
        {
            var from = path[index - 1];
            var to = path[index];
            var length = Distance(from, to);

            if (remaining < length)
            {
                return AlongSegment(from, to, remaining / length);
            }

            remaining -= length;
        }

        var destination = path[^1];

        return new Placement(destination.X, destination.Y, settledAngle);
    }

    private static Placement AlongSegment(Point from, Point to, double fraction) =>
        new(
            from.X + (to.X - from.X) * fraction,
            from.Y + (to.Y - from.Y) * fraction,
            Heading(from, to)
        );

    private static double Distance(Point from, Point to) =>
        Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));

    private static double Heading(Point from, Point to)
    {
        var angle = Math.Atan2(to.X - from.X, from.Y - to.Y);

        return angle < 0 ? angle + 2 * Math.PI : angle;
    }
}
