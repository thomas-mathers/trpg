using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class RoomLayoutPass
{
    private static readonly IReadOnlyDictionary<Guid, DistrictBuildingLayout> NoBuildingLayouts =
        new Dictionary<Guid, DistrictBuildingLayout>();

    internal static void Run(
        LocationLayoutContext context,
        Dictionary<Guid, ConnectorExit> exitByConnectorId
    )
    {
        var classifier = new ConnectorExitClassifier(context, NoBuildingLayouts);

        foreach (var location in context.Locations.Where(l => l.Kind == LocationKind.Room))
        {
            LayOutRoom(context, classifier, location, exitByConnectorId);
        }
    }

    private static void LayOutRoom(
        LocationLayoutContext context,
        ConnectorExitClassifier classifier,
        Location location,
        Dictionary<Guid, ConnectorExit> exitByConnectorId
    )
    {
        var random = new Random(LayoutSeed.From(location.Id));
        var props = context.PropsByLocationId[location.Id].ToArray();
        var inputs = props
            .Select(prop => new RoomPropInput(prop.Id, PropModelResolver.Resolve(prop)))
            .ToArray();
        var requests = context
            .ConnectorsByOrigin[location.Id]
            .Select(classifier.Classify)
            .ToArray();
        var initial = InitialSize(context, context.RoomByLocationId[location.Id], inputs, random);

        var result = RoomPropPlacer.Place(initial, inputs, requests, random);

        location.Width = result.Room.Width;
        location.Depth = result.Room.Depth;
        ApplyProps(props, result.Props);

        foreach (var exit in result.Exits)
        {
            exitByConnectorId[exit.ConnectorId] = exit;
        }
    }

    private static Footprint InitialSize(
        LocationLayoutContext context,
        Room room,
        IReadOnlyCollection<RoomPropInput> inputs,
        Random random
    )
    {
        if (LocationLayoutContext.IsHallway(room))
        {
            return LocationSizer.SizeHallway(
                context
                    .RoomsByBuilding[room.BuildingId]
                    .Count(other =>
                        other.FloorNumber == room.FloorNumber
                        && !LocationLayoutContext.IsHallway(other)
                    )
            );
        }

        return LocationSizer.SizeRoom(
            new RoomSizingRequest(
                context.BuildingById[room.BuildingId].BuildingType,
                room.Role,
                inputs.Select(input => input.Model).ToArray(),
                room.Capacity
            ),
            random
        );
    }

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
}
