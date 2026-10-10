using TRPG.Domain.Models;

namespace TRPG.Application.Common.Navigation;

public record MovementInput(double Forward, double Strafe, double Heading);

public record MovementObstacle(double X, double Y, double Angle, double Width, double Depth);

public record MovementSpace(
    double Width,
    double Depth,
    IReadOnlyCollection<MovementObstacle> Obstacles
);

public static class PlayerMovementIntegrator
{
    public const double PlayerRadius = 0.35;

    private const double BoundsMargin = 0.3;
    private const int ResolvePasses = 2;
    private const double MaximumStepSeconds = 1.0 / 30;

    public static Point Advance(
        Point position,
        MovementInput input,
        double speed,
        TimeSpan elapsed,
        MovementSpace space
    )
    {
        var remaining = elapsed.TotalSeconds;
        while (remaining > 0)
        {
            var step = Math.Min(remaining, MaximumStepSeconds);
            position = Step(position, input, speed * step, space);
            remaining -= step;
        }

        return position;
    }

    private static Point Step(
        Point position,
        MovementInput input,
        double distance,
        MovementSpace space
    )
    {
        var (sin, cos) = Math.SinCos(input.Heading);
        var attempted = new Point(
            position.X + (((input.Forward * sin) + (input.Strafe * cos)) * distance),
            position.Y + (((-input.Forward * cos) + (input.Strafe * sin)) * distance)
        );

        return ClampToBounds(PushOutOfObstacles(attempted, space.Obstacles), space);
    }

    private static Point PushOutOfObstacles(
        Point point,
        IReadOnlyCollection<MovementObstacle> obstacles
    )
    {
        for (var pass = 0; pass < ResolvePasses; pass++)
        {
            foreach (var obstacle in obstacles)
            {
                point = PushOutOfObstacle(point, obstacle);
            }
        }

        return point;
    }

    private static Point PushOutOfObstacle(Point point, MovementObstacle obstacle)
    {
        var (sin, cos) = Math.SinCos(obstacle.Angle);
        var offsetX = point.X - obstacle.X;
        var offsetY = point.Y - obstacle.Y;
        var local = new Point(
            (offsetX * cos) + (offsetY * sin),
            (-offsetX * sin) + (offsetY * cos)
        );
        var pushed = PushOutOfRectangle(local, obstacle.Width / 2, obstacle.Depth / 2);

        if (pushed == local)
        {
            return point;
        }

        return new Point(
            obstacle.X + (pushed.X * cos) - (pushed.Y * sin),
            obstacle.Y + (pushed.X * sin) + (pushed.Y * cos)
        );
    }

    private static Point PushOutOfRectangle(Point point, double halfWidth, double halfDepth)
    {
        var gapX = point.X - Math.Clamp(point.X, -halfWidth, halfWidth);
        var gapY = point.Y - Math.Clamp(point.Y, -halfDepth, halfDepth);
        var distance = Math.Sqrt((gapX * gapX) + (gapY * gapY));

        if (distance >= PlayerRadius)
        {
            return point;
        }

        if (distance > 0)
        {
            var scale = PlayerRadius / distance;
            return new Point(point.X - gapX + (gapX * scale), point.Y - gapY + (gapY * scale));
        }

        return EjectThroughNearestFace(point, halfWidth, halfDepth);
    }

    private static Point EjectThroughNearestFace(Point point, double halfWidth, double halfDepth)
    {
        var toRight = halfWidth - point.X;
        var toLeft = point.X + halfWidth;
        var toBottom = halfDepth - point.Y;
        var toTop = point.Y + halfDepth;
        var nearest = Math.Min(Math.Min(toRight, toLeft), Math.Min(toBottom, toTop));

        if (nearest == toRight)
        {
            return new Point(halfWidth + PlayerRadius, point.Y);
        }

        if (nearest == toLeft)
        {
            return new Point(-halfWidth - PlayerRadius, point.Y);
        }

        if (nearest == toBottom)
        {
            return new Point(point.X, halfDepth + PlayerRadius);
        }

        return new Point(point.X, -halfDepth - PlayerRadius);
    }

    private static Point ClampToBounds(Point point, MovementSpace space) =>
        new(ClampAxis(point.X, space.Width), ClampAxis(point.Y, space.Depth));

    private static double ClampAxis(double value, double extent)
    {
        var minimum = BoundsMargin;
        var maximum = extent - BoundsMargin;

        return maximum < minimum ? (minimum + maximum) / 2 : Math.Clamp(value, minimum, maximum);
    }
}
