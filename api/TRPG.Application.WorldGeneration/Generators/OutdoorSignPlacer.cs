using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class OutdoorSignPlacer
{
    private const double NameSignWidth = 1.2;
    private const double ExitSignWidth = 1;
    private const double SignDepth = 0.2;
    private const double FacadeGap = 0.05;
    private const double DoorSideOffset = 1.8;
    private const double FacadeEndMargin = 0.2;
    private const double ExitInset = 1.2;
    private const double ExitSideOffset = 3;

    internal static IReadOnlyList<Sign> Place(
        LocationLayoutContext context,
        Location district,
        IReadOnlyDictionary<Guid, DistrictBuildingLayout> buildingLayouts,
        IReadOnlyCollection<DistrictDecor> decor,
        IReadOnlyCollection<ConnectorExit> exits
    )
    {
        var buildings = context.BuildingsByExterior[district.Id].ToArray();
        var obstacles = Obstacles(context, district, buildings, buildingLayouts, decor);
        var signs = new List<Sign>();

        foreach (var building in buildings.OrderBy(building => building.Id))
        {
            var placement = NameSignPlacement(building, obstacles, district);
            AddIfPlaced(signs, obstacles, district, building.Name, NameSignWidth, placement);
        }

        foreach (var exit in exits.OrderBy(exit => exit.ConnectorId))
        {
            var text = ExitText(context, exit.ConnectorId);
            var placement = text is null ? null : ExitSignPlacement(exit, obstacles, district);
            AddIfPlaced(signs, obstacles, district, text ?? "", ExitSignWidth, placement);
        }

        return signs;
    }

    private static void AddIfPlaced(
        List<Sign> signs,
        List<OrientedBox> obstacles,
        Location district,
        string text,
        double width,
        Placement? placement
    )
    {
        if (placement is null)
        {
            return;
        }

        obstacles.Add(OrientedBox.From(placement, new Footprint(width, SignDepth)));
        signs.Add(
            new Sign
            {
                LocationId = district.Id,
                WorldId = district.WorldId,
                Name = "Sign",
                Description = text,
                X = placement.X,
                Y = placement.Y,
                Angle = placement.Angle,
                Width = width,
                Depth = SignDepth,
            }
        );
    }

    private static Placement? NameSignPlacement(
        Building building,
        IReadOnlyList<OrientedBox> obstacles,
        Location district
    )
    {
        var (sin, cos) = Math.SinCos(building.Angle);
        var ahead = building.Depth / 2 + SignDepth / 2 + FacadeGap;
        var limit = building.Width / 2 - FacadeEndMargin - NameSignWidth / 2;

        return new[] { DoorSideOffset, -DoorSideOffset }
            .Where(along => Math.Abs(along) <= limit)
            .Select(along => new Placement(
                building.X + along * cos + ahead * sin,
                building.Y + along * sin - ahead * cos,
                building.Angle
            ))
            .FirstOrDefault(placement => IsFree(placement, NameSignWidth, obstacles, district));
    }

    private static Placement? ExitSignPlacement(
        ConnectorExit exit,
        IReadOnlyList<OrientedBox> obstacles,
        Location district
    )
    {
        var (sin, cos) = Math.SinCos(exit.FacingAngle);

        return new[] { ExitSideOffset, -ExitSideOffset }
            .Select(side => new Placement(
                exit.Point.X + ExitInset * sin + side * cos,
                exit.Point.Y - ExitInset * cos + side * sin,
                exit.FacingAngle
            ))
            .FirstOrDefault(placement => IsFree(placement, ExitSignWidth, obstacles, district));
    }

    private static string? ExitText(LocationLayoutContext context, Guid connectorId)
    {
        var connector = context.Connectors.Single(candidate => candidate.Id == connectorId);
        var destination = context.LocationById[connector.DestinationLocationId];

        return destination.Kind is LocationKind.District or LocationKind.Wilderness
            ? $"To {connector.DestinationLabel}"
            : null;
    }

    private static bool IsFree(
        Placement placement,
        double width,
        IReadOnlyList<OrientedBox> obstacles,
        Location district
    )
    {
        var box = OrientedBox.From(placement, new Footprint(width, SignDepth));

        return box.IsInside(district.Width, district.Depth)
            && obstacles.All(obstacle => !box.Overlaps(obstacle));
    }

    private static List<OrientedBox> Obstacles(
        LocationLayoutContext context,
        Location district,
        IReadOnlyCollection<Building> buildings,
        IReadOnlyDictionary<Guid, DistrictBuildingLayout> buildingLayouts,
        IReadOnlyCollection<DistrictDecor> decor
    ) =>
        buildings
            .Select(building =>
                OrientedBox.From(
                    new Placement(building.X, building.Y, building.Angle),
                    new Footprint(building.Width, building.Depth)
                )
            )
            .Concat(
                buildings.Select(building =>
                    OutdoorFurnisher.Approach(buildingLayouts[building.Id])
                )
            )
            .Concat(decor.Select(item => OrientedBox.From(item.Placement, item.Footprint)))
            .Concat(
                context
                    .PropsByLocationId[district.Id]
                    .Select(prop =>
                        OrientedBox.From(
                            new Placement(prop.X, prop.Y, prop.Angle),
                            new Footprint(prop.Width, prop.Depth)
                        )
                    )
            )
            .ToList();
}
