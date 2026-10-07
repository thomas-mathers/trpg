using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public static class RoomNavigationGrid
{
    public const double CellSize = 0.5;

    private static readonly HashSet<PropModel> NonBlockingModels =
    [
        PropModel.FurnitureRug,
        PropModel.FurnitureChandelier,
        PropModel.FurnitureWallSconce,
        PropModel.FurnitureWallLantern,
        PropModel.FurnitureBanner,
    ];

    public static NavigationGrid Build(Location room, IEnumerable<Prop> props)
    {
        var obstacles = props.Where(BlocksFloor).Select(BoundsOf).ToArray();

        return new NavigationGrid(
            room.Width,
            room.Depth,
            CellSize,
            centre => obstacles.Any(obstacle => obstacle.Contains(centre))
        );
    }

    private static bool BlocksFloor(Prop prop) =>
        prop is not (Sign or Trap or Trigger)
        && !NonBlockingModels.Contains(PropModelResolver.Resolve(prop));

    private static FloorBounds BoundsOf(Prop prop)
    {
        var (sin, cos) = Math.SinCos(prop.Angle);
        var halfWidth = (Math.Abs(cos) * prop.Width + Math.Abs(sin) * prop.Depth) / 2;
        var halfDepth = (Math.Abs(sin) * prop.Width + Math.Abs(cos) * prop.Depth) / 2;

        return new FloorBounds(
            prop.X - halfWidth,
            prop.X + halfWidth,
            prop.Y - halfDepth,
            prop.Y + halfDepth
        );
    }

    private readonly record struct FloorBounds(double Left, double Right, double Top, double Bottom)
    {
        public bool Contains(Point point) =>
            point.X > Left && point.X < Right && point.Y > Top && point.Y < Bottom;
    }
}
