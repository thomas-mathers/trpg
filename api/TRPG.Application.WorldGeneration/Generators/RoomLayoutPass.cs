using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class RoomLayoutPass
{
    private static readonly IReadOnlyDictionary<Guid, DistrictBuildingLayout> NoBuildingLayouts =
        new Dictionary<Guid, DistrictBuildingLayout>();

    internal static IReadOnlyList<Prop> Run(
        LocationLayoutContext context,
        Dictionary<Guid, ConnectorExit> exitByConnectorId
    )
    {
        var classifier = new ConnectorExitClassifier(context, NoBuildingLayouts);
        var furniture = new List<Prop>();

        foreach (var location in context.Locations.Where(l => l.Kind == LocationKind.Room))
        {
            var layout = LayOutRoom(context, classifier, location);

            ApplyResult(location, layout.Result, exitByConnectorId);
            furniture.AddRange(layout.Furniture);
        }

        return furniture;
    }

    private static RoomLayout LayOutRoom(
        LocationLayoutContext context,
        ConnectorExitClassifier classifier,
        Location location
    )
    {
        var room = context.RoomByLocationId[location.Id];
        var building = context.BuildingById[room.BuildingId];
        var props = context.PropsByLocationId[location.Id].ToArray();
        var inputs = props
            .Select(prop => new RoomPropInput(prop.Id, PropModelResolver.Resolve(prop)))
            .ToArray();
        var requests = context
            .ConnectorsByOrigin[location.Id]
            .Select(classifier.Classify)
            .ToArray();
        var recipe = RoomRecipeCatalog.Find(building.BuildingType, room.Name);

        if (recipe is null)
        {
            var scattered = PlaceScattered(context, room, building, inputs, requests);
            ApplyProps(props, scattered.Props);

            return new RoomLayout(scattered, []);
        }

        var size = TemplateSize(context, room, building);
        var exits = ConnectorPointResolver.ResolveExits(size, requests);
        var furnished = RoomFurnisher.Furnish(
            size,
            recipe,
            inputs,
            exits.Select(RoomFurnisher.KeepOut).ToArray()
        );
        ApplyProps(props, furnished.Bound);

        return new RoomLayout(
            new RoomPlacementResult(size, exits, furnished.Bound),
            furnished.Decor.Select(item => CreateFurniture(location, item)).ToArray()
        );
    }

    private static RoomPlacementResult PlaceScattered(
        LocationLayoutContext context,
        Room room,
        Building building,
        IReadOnlyCollection<RoomPropInput> inputs,
        IReadOnlyCollection<ConnectorExitRequest> requests
    )
    {
        var random = new Random(LayoutSeed.From(room.LocationId));
        var initial = BuildingTypes.Dungeon.Contains(building.BuildingType)
            ? LocationSizer.SizeRoom(
                new RoomSizingRequest(
                    building.BuildingType,
                    room.Role,
                    inputs.Select(input => input.Model).ToArray(),
                    room.Capacity
                ),
                random
            )
            : TemplateSize(context, room, building);

        return RoomPropPlacer.Place(initial, inputs, requests, random);
    }

    private static Footprint TemplateSize(
        LocationLayoutContext context,
        Room room,
        Building building
    ) =>
        BuildingTemplateCatalog
            .Resolve(building.BuildingType, context.RoomsByBuilding[building.Id].ToArray())
            .RoomSize(room);

    private static void ApplyResult(
        Location location,
        RoomPlacementResult result,
        Dictionary<Guid, ConnectorExit> exitByConnectorId
    )
    {
        location.Width = result.Room.Width;
        location.Depth = result.Room.Depth;

        foreach (var exit in result.Exits)
        {
            exitByConnectorId[exit.ConnectorId] = exit;
        }
    }

    private static Prop CreateFurniture(Location location, RecipeItem item) =>
        item.IsSeat ? CreateSeat(location, item) : CreateDecor(location, item);

    private static Seat CreateSeat(Location location, RecipeItem item) =>
        new()
        {
            LocationId = location.Id,
            WorldId = location.WorldId,
            Name = PropModelNames
                .DisplayName(item.Model)
                .Replace("Seat ", "", StringComparison.Ordinal),
            X = item.Placement.X,
            Y = item.Placement.Y,
            Angle = item.Placement.Angle,
            Width = item.Footprint.Width,
            Depth = item.Footprint.Depth,
        };

    private static Furniture CreateDecor(Location location, RecipeItem item) =>
        new()
        {
            LocationId = location.Id,
            WorldId = location.WorldId,
            Name = PropModelNames.DisplayName(item.Model),
            Model = item.Model,
            X = item.Placement.X,
            Y = item.Placement.Y,
            Angle = item.Placement.Angle,
            Width = item.Footprint.Width,
            Depth = item.Footprint.Depth,
        };

    private static void ApplyProps(Prop[] props, IReadOnlyList<PlacedProp> placed)
    {
        var placedById = placed.ToDictionary(prop => prop.Id);

        foreach (var prop in props)
        {
            var pose = placedById[prop.Id];
            prop.X = pose.Placement.X;
            prop.Y = pose.Placement.Y;
            prop.Angle = pose.Placement.Angle;
            prop.Width = pose.Footprint.Width;
            prop.Depth = pose.Footprint.Depth;
        }
    }

    private record RoomLayout(RoomPlacementResult Result, IReadOnlyList<Prop> Furniture);
}
