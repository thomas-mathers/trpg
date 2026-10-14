using TRPG.Domain.Models;

namespace TRPG.Application.Abilities;

public static class AbilityFacing
{
    public const double ArcRadians = Math.PI;

    public static double HeadingToward(Point origin, Point target) =>
        Math.Atan2(target.X - origin.X, -(target.Y - origin.Y));

    public static bool IsFacing(Point origin, double heading, Point target)
    {
        var dx = target.X - origin.X;
        var dy = target.Y - origin.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length == 0)
        {
            return true;
        }

        var dot = (Math.Sin(heading) * dx / length) + (-Math.Cos(heading) * dy / length);

        return dot >= Math.Cos(ArcRadians / 2);
    }
}
