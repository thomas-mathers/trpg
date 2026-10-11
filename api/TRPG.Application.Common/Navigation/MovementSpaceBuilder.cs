using TRPG.Domain.Models;

namespace TRPG.Application.Common.Navigation;

public static class MovementSpaceBuilder
{
    public const double StairWidth = 1.2;
    public const double StairDepth = 2;

    public static MovementSpace Build(
        Location location,
        IEnumerable<Prop> props,
        IEnumerable<Building> buildings,
        IEnumerable<PlacedConnector> connectors
    ) =>
        new(
            location.Width,
            location.Depth,
            [
                .. props.Where(prop => prop.BlocksMovement).Select(ToObstacle),
                .. buildings.Select(ToObstacle),
                .. connectors
                    .Where(placed => placed.Connector.StairDirection != null)
                    .Select(ToStairObstacle),
            ]
        );

    private static MovementObstacle ToObstacle(Prop prop) =>
        new(prop.X, prop.Y, prop.Angle, prop.Width, prop.Depth);

    private static MovementObstacle ToObstacle(Building building) =>
        new(building.X, building.Y, building.Angle, building.Width, building.Depth);

    private static MovementObstacle ToStairObstacle(PlacedConnector placed)
    {
        var angle = placed.Connector.ExitAngle;
        var (sin, cos) = Math.SinCos(angle);

        return new MovementObstacle(
            placed.Exit.X + (sin * StairDepth / 2),
            placed.Exit.Y - (cos * StairDepth / 2),
            angle,
            StairWidth,
            StairDepth
        );
    }
}
