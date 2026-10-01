using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal sealed class ConnectorExitClassifier(
    LocationLayoutContext context,
    IReadOnlyDictionary<Guid, DistrictBuildingLayout> buildingLayouts
)
{
    internal ConnectorExitRequest Classify(LocationConnector connector)
    {
        if (connector.Direction is { } direction)
        {
            return Request(connector, ConnectorExitKind.Compass) with { Direction = direction };
        }

        var origin = context.LocationById[connector.OriginLocationId];

        return origin.Kind == LocationKind.Room
            ? ClassifyFromRoom(connector, origin)
            : ClassifyFromExterior(connector, origin);
    }

    private ConnectorExitRequest ClassifyFromRoom(LocationConnector connector, Location origin)
    {
        var room = context.RoomByLocationId[origin.Id];

        if (!context.RoomByLocationId.TryGetValue(connector.DestinationLocationId, out var other))
        {
            return Request(connector, ConnectorExitKind.SouthDoor);
        }

        if (other.BuildingId != room.BuildingId)
        {
            return Bearing(connector);
        }

        if (other.FloorNumber != room.FloorNumber)
        {
            return Request(connector, StairsKind(room, other));
        }

        if (LocationLayoutContext.IsHallway(room))
        {
            return Request(connector, ConnectorExitKind.SideDoor) with
            {
                SideIndex = SideDoorIndex(connector, room),
            };
        }

        return LocationLayoutContext.IsHallway(other)
            ? Request(connector, ConnectorExitKind.SouthDoor)
            : Bearing(connector);
    }

    private ConnectorExitRequest ClassifyFromExterior(LocationConnector connector, Location origin)
    {
        if (TryFindDoor(connector, origin, out var door))
        {
            return Request(connector, ConnectorExitKind.Fixed) with
            {
                FixedPoint = door.DoorPoint,
                FixedFacingAngle = door.Placement.Angle,
            };
        }

        var destination = context.LocationById[connector.DestinationLocationId];

        return origin.Kind == LocationKind.Wilderness && destination.Kind == LocationKind.Wilderness
            ? BearingTowardState(connector, origin, destination)
            : Bearing(connector);
    }

    private bool TryFindDoor(
        LocationConnector connector,
        Location origin,
        out DistrictBuildingLayout door
    )
    {
        door = null!;

        return context.RoomByLocationId.TryGetValue(connector.DestinationLocationId, out var room)
            && context.BuildingById[room.BuildingId].ExteriorLocationId == origin.Id
            && buildingLayouts.TryGetValue(room.BuildingId, out door!);
    }

    private ConnectorExitRequest BearingTowardState(
        LocationConnector connector,
        Location origin,
        Location destination
    )
    {
        if (
            !context.StateById.TryGetValue(origin.StateId, out var from)
            || !context.StateById.TryGetValue(destination.StateId, out var to)
            || from.Center == to.Center
        )
        {
            return Bearing(connector);
        }

        var bearing = Math.Atan2(to.Center.X - from.Center.X, -(to.Center.Y - from.Center.Y));

        return Request(connector, ConnectorExitKind.Bearing) with
        {
            BearingRadians = bearing,
        };
    }

    private ConnectorExitRequest Bearing(LocationConnector connector) =>
        Request(connector, ConnectorExitKind.Bearing) with
        {
            BearingRadians = LayoutSeed.PairBearing(
                connector.OriginLocationId,
                connector.DestinationLocationId
            ),
        };

    private int SideDoorIndex(LocationConnector connector, Room hallway)
    {
        var sideConnectors = context
            .ConnectorsByOrigin[connector.OriginLocationId]
            .Where(candidate =>
                context.RoomByLocationId.TryGetValue(candidate.DestinationLocationId, out var other)
                && other.BuildingId == hallway.BuildingId
                && other.FloorNumber == hallway.FloorNumber
            )
            .OrderBy(candidate => candidate.DestinationLocationId)
            .ToList();

        return sideConnectors.IndexOf(connector);
    }

    private static ConnectorExitKind StairsKind(Room room, Room other)
    {
        if (LocationLayoutContext.IsHallway(room))
        {
            return ConnectorExitKind.NorthStairs;
        }

        return other.FloorNumber > room.FloorNumber
            ? ConnectorExitKind.NorthEastStairs
            : ConnectorExitKind.NorthWestStairs;
    }

    private static ConnectorExitRequest Request(
        LocationConnector connector,
        ConnectorExitKind kind
    ) => new(connector.Id, connector.DestinationLocationId, kind);
}
