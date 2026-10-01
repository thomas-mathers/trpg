using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class ExteriorLayoutPass
{
    internal static void Run(
        LocationLayoutContext context,
        Dictionary<Guid, ConnectorExit> exitByConnectorId
    )
    {
        foreach (var location in context.Locations.Where(l => l.Kind != LocationKind.Room))
        {
            var buildingLayouts = LayOut(context, location);
            var classifier = new ConnectorExitClassifier(context, buildingLayouts);
            var requests = context
                .ConnectorsByOrigin[location.Id]
                .Select(classifier.Classify)
                .ToArray();
            var frame = new Footprint(Width: location.Width, Depth: location.Depth);

            foreach (var exit in ConnectorPointResolver.ResolveExits(frame, requests))
            {
                exitByConnectorId[exit.ConnectorId] = exit;
            }
        }
    }

    private static Dictionary<Guid, DistrictBuildingLayout> LayOut(
        LocationLayoutContext context,
        Location location
    )
    {
        var buildings = context.BuildingsByExterior[location.Id].ToArray();
        var footprints = buildings.ToDictionary(
            building => building.Id,
            building => SizeBuilding(context, building)
        );
        var inputs = buildings
            .Select(building => new DistrictBuildingInput(building.Id, footprints[building.Id]))
            .ToArray();
        var layouts =
            location.Kind == LocationKind.District
                ? LayOutDistrict(context, location, inputs)
                : LayOutWilderness(location, inputs);

        foreach (var building in buildings)
        {
            var layout = layouts[building.Id];
            building.X = layout.Placement.X;
            building.Y = layout.Placement.Y;
            building.Angle = layout.Placement.Angle;
            building.Width = footprints[building.Id].Width;
            building.Depth = footprints[building.Id].Depth;
        }

        return layouts;
    }

    private static Dictionary<Guid, DistrictBuildingLayout> LayOutDistrict(
        LocationLayoutContext context,
        Location location,
        DistrictBuildingInput[] buildings
    )
    {
        var props = context.PropsByLocationId[location.Id].ToArray();
        var seats = props
            .Select(prop => new DistrictSeatInput(
                prop.Id,
                PropFootprintCatalog.Get(PropModelResolver.Resolve(prop)).Footprint
            ))
            .ToArray();

        var layout = DistrictLayoutGenerator.Generate(buildings, seats);

        location.Width = layout.District.Width;
        location.Depth = layout.District.Depth;
        ApplySeats(props, seats, layout.Seats);

        return layout.Buildings.ToDictionary(building => building.Id);
    }

    private static Dictionary<Guid, DistrictBuildingLayout> LayOutWilderness(
        Location location,
        DistrictBuildingInput[] buildings
    )
    {
        var size = LocationSizer.SizeWilderness();
        location.Width = size.Width;
        location.Depth = size.Depth;

        return WildernessBuildingPlacer
            .Place(size, buildings, new Random(LayoutSeed.From(location.Id)))
            .ToDictionary(building => building.Id);
    }

    private static void ApplySeats(
        Prop[] props,
        DistrictSeatInput[] inputs,
        IReadOnlyList<DistrictSeatLayout> layouts
    )
    {
        var layoutById = layouts.ToDictionary(layout => layout.Id);
        var footprintById = inputs.ToDictionary(input => input.Id, input => input.Footprint);

        foreach (var prop in props)
        {
            var placement = layoutById[prop.Id].Placement;
            prop.X = placement.X;
            prop.Y = placement.Y;
            prop.Angle = placement.Angle;
            prop.Width = footprintById[prop.Id].Width;
            prop.Depth = footprintById[prop.Id].Depth;
        }
    }

    private static Footprint SizeBuilding(LocationLayoutContext context, Building building)
    {
        var rooms = context.RoomsByBuilding[building.Id].ToArray();
        var groundFloor = rooms.Where(room => room.FloorNumber == 0).ToArray();
        var groundFloorArea = (groundFloor.Length > 0 ? groundFloor : rooms).Sum(room =>
        {
            var location = context.LocationById[room.LocationId];
            return location.Width * location.Depth;
        });

        return LocationSizer.SizeBuilding(
            building.BuildingType,
            groundFloorArea,
            new Random(LayoutSeed.From(building.Id))
        );
    }
}
