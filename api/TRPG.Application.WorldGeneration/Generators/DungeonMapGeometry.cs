using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DungeonRoomDimensions(int Width, int Height);

internal static class DungeonMapGeometry
{
    public static Rectangle BoundsFor(RoomRole role, Point center, Random random)
    {
        var dimensions = DimensionsFor(role);
        var width = 2 * (int)Math.Ceiling((dimensions.Width + random.Next(-2, 3)) / 2d);
        var height = 2 * (int)Math.Ceiling((dimensions.Height + random.Next(-1, 2)) / 2d);
        var left = (int)Math.Round(center.X - width / 2d);
        var top = (int)Math.Round(center.Y - height / 2d);

        return new Rectangle(left, top, left + width, top + height);
    }

    public static Rectangle BoundsAt(Point center, int width, int height)
    {
        var left = (int)Math.Round(center.X - width / 2d);
        var top = (int)Math.Round(center.Y - height / 2d);
        return new Rectangle(left, top, left + width, top + height);
    }

    public static Point Center(Rectangle bounds) =>
        new((bounds.Left + bounds.Right) / 2d, (bounds.Top + bounds.Bottom) / 2d);

    public static Polyline PathBetween(Rectangle origin, Rectangle destination)
    {
        var originCenter = Center(origin);
        var destinationCenter = Center(destination);
        var horizontalDistance = destinationCenter.X - originCenter.X;
        var verticalDistance = destinationCenter.Y - originCenter.Y;

        return Math.Abs(horizontalDistance) >= Math.Abs(verticalDistance)
            ? HorizontalPath(origin, destination, originCenter, destinationCenter)
            : VerticalPath(origin, destination, originCenter, destinationCenter);
    }

    public static Polyline PathBelow(
        Rectangle origin,
        Rectangle destination,
        IReadOnlyCollection<Rectangle> roomBounds
    )
    {
        var originCenter = Center(origin);
        var destinationCenter = Center(destination);
        var sameLane = roomBounds
            .Where(bounds => bounds.Top <= origin.Bottom && bounds.Bottom >= origin.Top)
            .ToArray();
        var laneBottom = sameLane.Max(bounds => bounds.Bottom);
        var roomsBelowOrigin = roomBounds.Where(bounds => bounds.Top > laneBottom).ToArray();
        var routeY =
            roomsBelowOrigin.Length == 0
                ? laneBottom + 14
                : (laneBottom + roomsBelowOrigin.Min(bounds => bounds.Top)) / 2d;
        var destinationDoorX = destinationCenter.X + 6;
        return new Polyline
        {
            Points =
            [
                new Point(originCenter.X, origin.Bottom),
                new Point(originCenter.X, routeY),
                new Point(destinationDoorX, routeY),
                new Point(destinationDoorX, destination.Bottom),
            ],
        };
    }

    public static Polyline Reverse(Polyline path) =>
        new()
        {
            Points = path
                .Points.AsEnumerable()
                .Reverse()
                .Select(point => new Point(point.X, point.Y))
                .ToList(),
        };

    private static Polyline HorizontalPath(
        Rectangle origin,
        Rectangle destination,
        Point originCenter,
        Point destinationCenter
    )
    {
        var movingRight = destinationCenter.X >= originCenter.X;
        var doorY = SharedDoorAxis(origin.Top, origin.Bottom, destination.Top, destination.Bottom);
        var start = new Point(movingRight ? origin.Right : origin.Left, doorY ?? originCenter.Y);
        var end = new Point(
            movingRight ? destination.Left : destination.Right,
            doorY ?? destinationCenter.Y
        );
        if (doorY.HasValue)
        {
            return new Polyline { Points = [start, end] };
        }

        var middleX = (start.X + end.X) / 2d;
        return new Polyline
        {
            Points = [start, new Point(middleX, start.Y), new Point(middleX, end.Y), end],
        };
    }

    private static Polyline VerticalPath(
        Rectangle origin,
        Rectangle destination,
        Point originCenter,
        Point destinationCenter
    )
    {
        var movingDown = destinationCenter.Y >= originCenter.Y;
        var doorX = SharedDoorAxis(origin.Left, origin.Right, destination.Left, destination.Right);
        var start = new Point(doorX ?? originCenter.X, movingDown ? origin.Bottom : origin.Top);
        var end = new Point(
            doorX ?? destinationCenter.X,
            movingDown ? destination.Top : destination.Bottom
        );
        if (doorX.HasValue)
        {
            return new Polyline { Points = [start, end] };
        }

        var middleY = (start.Y + end.Y) / 2d;
        return new Polyline
        {
            Points = [start, new Point(start.X, middleY), new Point(end.X, middleY), end],
        };
    }

    private static double? SharedDoorAxis(
        double originMinimum,
        double originMaximum,
        double destinationMinimum,
        double destinationMaximum
    )
    {
        var minimum = Math.Max(originMinimum, destinationMinimum) + 3;
        var maximum = Math.Min(originMaximum, destinationMaximum) - 3;
        return minimum <= maximum
            ? Math.Clamp(
                (originMinimum + originMaximum + destinationMinimum + destinationMaximum) / 4,
                minimum,
                maximum
            )
            : null;
    }

    private static DungeonRoomDimensions DimensionsFor(RoomRole role) =>
        role switch
        {
            RoomRole.Entrance => new DungeonRoomDimensions(28, 16),
            RoomRole.BossChamber => new DungeonRoomDimensions(40, 25),
            RoomRole.Passage => new DungeonRoomDimensions(22, 11),
            RoomRole.GuardPost => new DungeonRoomDimensions(25, 15),
            RoomRole.Storeroom => new DungeonRoomDimensions(28, 17),
            RoomRole.TreasureRoom => new DungeonRoomDimensions(31, 19),
            RoomRole.Shrine => new DungeonRoomDimensions(28, 18),
            RoomRole.Study => new DungeonRoomDimensions(25, 16),
            RoomRole.CellBlock => new DungeonRoomDimensions(34, 16),
            RoomRole.CollapsedGallery => new DungeonRoomDimensions(36, 21),
            RoomRole.FloodedSump => new DungeonRoomDimensions(34, 22),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
}
