using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class DistrictRoadInputs
{
    private static readonly HashSet<PropModel> Landmarks =
    [
        PropModel.FurnitureNoticeBoard,
        PropModel.FurnitureFountain,
        PropModel.FurnitureWell,
        PropModel.FurnitureFirePit,
        PropModel.FurnitureStatue,
        PropModel.FurnitureMonument,
        PropModel.FurnitureShrine,
        PropModel.FurnitureWaystone,
    ];

    internal static IReadOnlyList<RoadBuilding> Obstacles(
        LocationLayoutContext context,
        Location district,
        IReadOnlyCollection<DistrictDecor> decor
    )
    {
        var buildings = context
            .BuildingsByExterior[district.Id]
            .Select(building => new RoadBuilding(
                new Placement(building.X, building.Y, building.Angle),
                new Footprint(building.Width, building.Depth)
            ));
        var landmarkDecor = decor
            .Where(item => Landmarks.Contains(item.Model))
            .Select(item => new RoadBuilding(item.Placement, item.Footprint));
        var landmarkProps = context
            .PropsByLocationId[district.Id]
            .Where(prop => Landmarks.Contains(PropModelResolver.Resolve(prop)))
            .Select(prop => new RoadBuilding(
                new Placement(prop.X, prop.Y, prop.Angle),
                new Footprint(prop.Width, prop.Depth)
            ));

        return [.. buildings, .. landmarkDecor, .. landmarkProps];
    }

    internal static IReadOnlyList<RoadTerminal> Terminals(
        LocationLayoutContext context,
        Location district,
        IReadOnlyCollection<ConnectorExit> exits
    )
    {
        var destinationByConnectorId = context
            .ConnectorsByOrigin[district.Id]
            .ToDictionary(
                connector => connector.Id,
                connector => context.LocationById[connector.DestinationLocationId]
            );

        return exits
            .Select(exit =>
                ToTerminal(
                    exit,
                    destinationByConnectorId[exit.ConnectorId].Kind != LocationKind.Room
                )
            )
            .ToArray();
    }

    private static RoadTerminal ToTerminal(ConnectorExit exit, bool isGate)
    {
        var (sin, cos) = Math.SinCos(exit.FacingAngle);
        var directionX = Math.Round(sin);
        var directionY = -Math.Round(cos);
        var entry = new Point(
            CityGrid.CentreOf(CityGrid.CellOf(exit.Point.X + directionX * CityGrid.HalfCell)),
            CityGrid.CentreOf(CityGrid.CellOf(exit.Point.Y + directionY * CityGrid.HalfCell))
        );
        var start = new Point(
            entry.X - directionX * CityGrid.HalfCell,
            entry.Y - directionY * CityGrid.HalfCell
        );

        return new RoadTerminal(exit.ConnectorId, isGate, start, entry);
    }
}
