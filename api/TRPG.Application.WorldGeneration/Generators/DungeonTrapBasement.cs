using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

// Shared per-dungeon basement, lazily built. Every falling trap in the same dungeon reuses the
// same Rubble Landing (and, for Mechanical, the same Trap Cellar), so there is exactly one
// basement per generator call rather than one per trap. Used by both the ambient trap system and
// the TrapGauntlet obstacle, so a falling trap behaves identically no matter which system placed
// it — the whole reason this lives in its own reusable class instead of two copies.
//
// Per the original design table, Collapse and Slope are climbable back, Mechanical is not, so a
// falling trap's destination is never an existing room picked off the layout — it's a real
// basement room, created here, one floor below the dungeon's main level. Collapse and Slope share
// one Rubble Landing with a way back up; Mechanical's Trap Cellar has no way out of its own and
// always connects onward into the Rubble Landing, so a Mechanical fall alone can never soft-lock
// even when no Collapse/Slope trap happens to exist in the same dungeon.
internal sealed class DungeonTrapBasement(Guid worldId, Guid stateId, Guid buildingId)
{
    private const int BasementFloorNumber = -1;

    private Location? _rubbleLandingLocation;
    private Room? _rubbleLandingRoom;
    private Location? _trapCellarLocation;

    public List<Room> Rooms { get; } = [];
    public List<Location> Locations { get; } = [];
    public List<LocationConnector> LocationConnectors { get; } = [];

    public Guid TargetFor(TrapKind kind, DungeonRoomPlacement source) =>
        kind switch
        {
            TrapKind.Collapse or TrapKind.Slope => RubbleLanding(source).Id,
            TrapKind.Mechanical => TrapCellar(source).Id,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private Location RubbleLanding(DungeonRoomPlacement climbBackTarget)
    {
        if (_rubbleLandingLocation != null)
        {
            return _rubbleLandingLocation;
        }

        var landing = AddRoom(
            "Rubble Landing",
            "Loose stone and old rubble slope up toward the way back.",
            DungeonMapGeometry.BoundsAt(SourceCenter(climbBackTarget), width: 22, height: 16)
        );
        _rubbleLandingLocation = landing.Location;
        _rubbleLandingRoom = landing.Room;

        // Only the way back up is walkable — the fall itself is a relocation, not a passage,
        // so there is nothing to walk down.
        LocationConnectors.Add(
            new LocationConnector
            {
                OriginLocationId = landing.Location.Id,
                Name = "Rubble Slope",
                Description = "A scramble back up over loose rubble.",
                DestinationLocationId = climbBackTarget.Room.LocationId,
                DestinationLabel = climbBackTarget.Room.Name,
                WorldId = worldId,
            }
        );

        return landing.Location;
    }

    private Location TrapCellar(DungeonRoomPlacement source)
    {
        if (_trapCellarLocation != null)
        {
            return _trapCellarLocation;
        }

        // Guarantee the way out exists before the cellar does — a Mechanical fall must never
        // be the only trap in a dungeon with nowhere to climb out from.
        var rubbleLanding = RubbleLanding(source);

        var landingCenter = DungeonMapGeometry.Center(_rubbleLandingRoom!.Bounds!);
        var cellar = AddRoom(
            "Trap Cellar",
            "No catch to climb, no rubble to scramble — only a passage leading on.",
            DungeonMapGeometry.BoundsAt(
                new Point(landingCenter.X + 38, landingCenter.Y),
                width: 20,
                height: 14
            )
        );
        _trapCellarLocation = cellar.Location;

        var path = DungeonMapGeometry.PathBetween(cellar.Room.Bounds!, _rubbleLandingRoom.Bounds!);
        LocationConnectors.Add(
            new LocationConnector
            {
                OriginLocationId = cellar.Location.Id,
                Name = "Passage",
                Description = "A low passage leading toward the sound of shifting stone.",
                DestinationLocationId = rubbleLanding.Id,
                DestinationLabel = "Rubble Landing",
                Path = path,
                WorldId = worldId,
            }
        );
        LocationConnectors.Add(
            new LocationConnector
            {
                OriginLocationId = rubbleLanding.Id,
                Name = "Passage",
                Description = "A low passage leading back into the dark.",
                DestinationLocationId = cellar.Location.Id,
                DestinationLabel = "Trap Cellar",
                Path = DungeonMapGeometry.Reverse(path),
                WorldId = worldId,
            }
        );

        return cellar.Location;
    }

    private sealed record AddedRoom(Room Room, Location Location);

    private AddedRoom AddRoom(string name, string description, Rectangle bounds)
    {
        var roomId = Guid.NewGuid();
        var location = LocationGenerator.Generate(worldId, stateId, roomId: roomId, name: name);
        var room = new Room
        {
            Id = roomId,
            Bounds = bounds,
            BuildingId = buildingId,
            LocationId = location.Id,
            Name = name,
            Description = description,
            FloorNumber = BasementFloorNumber,
            WorldId = worldId,
        };

        Locations.Add(location);
        Rooms.Add(room);
        return new AddedRoom(room, location);
    }

    private static Point SourceCenter(DungeonRoomPlacement source) =>
        source.Room.Bounds == null
            ? new Point(source.DepthFromEntrance * 40, source.FloorNumber * 40)
            : DungeonMapGeometry.Center(source.Room.Bounds);
}
