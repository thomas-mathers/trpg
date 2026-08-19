using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Generators;

internal record DungeonGeneratorInput(
    IReadOnlyCollection<string> ExcludedNames,
    Location WildernessLocation,
    Guid WorldId
);

internal record DungeonGeneratorResult(
    Building Building,
    Room Room,
    Location Location,
    LocationConnector FrontDoor,
    DoorConnector Door
);

internal static class DungeonGenerator
{
    private static readonly IReadOnlyCollection<BuildingType> DungeonBuildingTypes =
    [
        BuildingType.Cave,
        BuildingType.Crypt,
        BuildingType.Mine,
        BuildingType.Ruins,
        BuildingType.Tower,
    ];

    private static readonly Dictionary<BuildingType, string[]> Names = new()
    {
        [BuildingType.Cave] =
        [
            "The Dark Hollow",
            "The Mossy Grotto",
            "The Black Maw",
            "The Dripping Cave",
            "The Stone Throat",
            "The Sunken Hollow",
            "The Narrow Den",
            "The Cold Cavern",
            "The Echoing Pit",
            "The Damp Recess",
        ],
        [BuildingType.Crypt] =
        [
            "The Sunken Crypt",
            "The Bone Vault",
            "The Silent Tomb",
            "The Sealed Burial",
            "The Forgotten Grave",
            "The Shroud Chamber",
            "The Pale Vault",
            "The Warding Tomb",
            "The Cold Rest",
            "The Hollow Mausoleum",
        ],
        [BuildingType.Mine] =
        [
            "The Old Shaft",
            "The Copper Vein",
            "The Abandoned Dig",
            "The Iron Seam",
            "The Broken Pickaxe",
            "The Salt Works",
            "The Deep Cut",
            "The Collapsed Tunnel",
            "The Ore Run",
            "The Grim Vein",
        ],
        [BuildingType.Ruins] =
        [
            "The Broken Hall",
            "The Fallen Spire",
            "The Shattered Court",
            "The Crumbled Keep",
            "The Ash Walls",
            "The Hollow Foundation",
            "The Rotted Estate",
            "The Scorched Shell",
            "The Sundered Gate",
            "The Old Rubble",
        ],
        [BuildingType.Tower] =
        [
            "The Crumbling Tower",
            "The Forsaken Spire",
            "The Watching Pillar",
            "The Leaning Turret",
            "The Black Spire",
            "The Warped Tower",
            "The Silent Watchtower",
            "The Broken Pinnacle",
            "The Pale Obelisk",
            "The Hollow Spire",
        ],
    };

    internal static readonly int TotalNameCount = Names.Values.Sum(names => names.Length);

    private static readonly Dictionary<BuildingType, string> RoomNames = new()
    {
        [BuildingType.Cave] = "Cave Entrance",
        [BuildingType.Crypt] = "Burial Chamber",
        [BuildingType.Mine] = "Mine Shaft",
        [BuildingType.Ruins] = "Ruined Hall",
        [BuildingType.Tower] = "Ground Floor",
    };

    public static DungeonGeneratorResult Generate(DungeonGeneratorInput input)
    {
        var availablePairs = DungeonBuildingTypes
            .SelectMany(type =>
                Names[type]
                    .Where(name => !input.ExcludedNames.Contains(name))
                    .Select(name => (Type: type, Name: name))
            )
            .ToArray();

        if (availablePairs.Length == 0)
        {
            throw new InvalidOperationException(
                $"No dungeon names left for wilderness location {input.WildernessLocation.Id} — the name pool ({TotalNameCount}) is exhausted."
            );
        }

        var (type, name) = availablePairs[Random.Shared.Next(availablePairs.Length)];

        var building = new Building
        {
            ExteriorLocationId = input.WildernessLocation.Id,
            BuildingType = type,
            Name = name,
            WorldId = input.WorldId,
        };
        var roomId = Guid.NewGuid();
        var location = LocationGenerator.Generate(
            input.WorldId,
            input.WildernessLocation.StateId,
            roomId: roomId
        );
        var room = new Room
        {
            Id = roomId,
            BuildingId = building.Id,
            LocationId = location.Id,
            Name = RoomNames[type],
            Description = "",
            FloorNumber = 0,
            WorldId = input.WorldId,
        };
        var frontDoor = new LocationConnector
        {
            OriginLocationId = room.LocationId,
            Name = "Front Door",
            Description = "The way back outside.",
            DestinationLocationId = input.WildernessLocation.Id,
            DestinationLabel = "Outside",
            WorldId = input.WorldId,
        };
        var door = new DoorConnector { ConnectorId = frontDoor.Id, WorldId = input.WorldId };
        return new DungeonGeneratorResult(building, room, location, frontDoor, door);
    }
}
