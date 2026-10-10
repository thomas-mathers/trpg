using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public static class LocationNavigationGrid
{
    public static NavigationGrid Build(
        Location location,
        IReadOnlyCollection<Prop> props,
        IReadOnlyCollection<Building> buildings
    )
    {
        if (location.Kind == LocationKind.Room)
        {
            return RoomNavigationGrid.Build(location, props);
        }

        var obstacles = props
            .Where(BlocksFloor)
            .Select(prop => Bounds(prop.X, prop.Y, prop.Angle, prop.Width, prop.Depth))
            .Concat(
                buildings.Select(building =>
                    Bounds(building.X, building.Y, building.Angle, building.Width, building.Depth)
                )
            )
            .ToArray();

        return new NavigationGrid(
            location.Width,
            location.Depth,
            RoomNavigationGrid.CellSize,
            point => obstacles.Any(obstacle => obstacle.Contains(point))
        );
    }

    private static bool BlocksFloor(Prop prop) =>
        prop is not (Sign or Trap or Trigger)
        && PropModelResolver.Resolve(prop)
            is not (
                PropModel.FurnitureRug
                or PropModel.FurnitureChandelier
                or PropModel.FurnitureWallSconce
                or PropModel.FurnitureWallLantern
                or PropModel.FurnitureBanner
            );

    private static RotatedBounds Bounds(
        double x,
        double y,
        double angle,
        double width,
        double depth
    ) => new(x, y, angle, width, depth);

    private readonly record struct RotatedBounds(
        double X,
        double Y,
        double Angle,
        double Width,
        double Depth
    )
    {
        public bool Contains(Point point)
        {
            var (sin, cos) = Math.SinCos(Angle);
            var offsetX = point.X - X;
            var offsetY = point.Y - Y;
            var localX = (offsetX * cos) + (offsetY * sin);
            var localY = (-offsetX * sin) + (offsetY * cos);

            return Math.Abs(localX) < Width / 2 && Math.Abs(localY) < Depth / 2;
        }
    }
}
