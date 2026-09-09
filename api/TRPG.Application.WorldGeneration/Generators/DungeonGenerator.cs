using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DungeonGeneratorInput(
    IReadOnlyCollection<string> ExcludedNames,
    Location WildernessLocation,
    Guid WorldId
)
{
    public Random Random { get; init; } = Random.Shared;
}

internal record DungeonRoomPlacement(Room Room, DungeonRoomRole Role, int DepthFromEntrance);

internal record DungeonGeneratorResult(
    Building Building,
    IReadOnlyList<DungeonRoomPlacement> Placements,
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Location> Locations,
    IReadOnlyList<LocationConnector> LocationConnectors,
    DoorConnector Door,
    Guid EntranceLocationId,
    Guid BossLocationId
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

    private static readonly string[] Qualifiers =
    [
        "Upper",
        "Lower",
        "Far",
        "Inner",
        "Outer",
        "Eastern",
        "Western",
        "Old",
        "Deep",
    ];

    private static readonly Dictionary<BuildingType, int> RoomCountByType = new()
    {
        [BuildingType.Cave] = 8,
        [BuildingType.Crypt] = 11,
        [BuildingType.Mine] = 13,
        [BuildingType.Ruins] = 12,
        [BuildingType.Tower] = 9,
    };

    public static DungeonGeneratorResult Generate(DungeonGeneratorInput input)
    {
        var (type, name) = ChooseNamedType(input);

        var building = new Building
        {
            ExteriorLocationId = input.WildernessLocation.Id,
            BuildingType = type,
            Name = name,
            WorldId = input.WorldId,
        };

        var layout = DungeonLayoutGenerator.Generate(
            new DungeonLayoutInput(RoomCountByType[type]) { Random = input.Random }
        );
        var assigned = DungeonRoleAssigner.Assign(layout, type, input.Random);

        var rooms = new List<Room>();
        var placements = new List<DungeonRoomPlacement>();
        var locations = new List<Location>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var room in assigned)
        {
            var content = DungeonRoomCatalog.ContentFor(room.Role, input.Random);
            var roomName = UniqueName(content.Name, usedNames, input.Random);
            var roomId = Guid.NewGuid();
            var location = LocationGenerator.Generate(
                input.WorldId,
                input.WildernessLocation.StateId,
                roomId: roomId
            );

            var built = new Room
            {
                Id = roomId,
                BuildingId = building.Id,
                LocationId = location.Id,
                Name = roomName,
                Description = content.Description,
                FloorNumber = 0,
                Position = room.Node.Position,
                WorldId = input.WorldId,
            };

            locations.Add(location);
            rooms.Add(built);
            placements.Add(new DungeonRoomPlacement(built, room.Role, room.Node.DepthFromEntrance));
        }

        var entranceLocationId = locations[layout.EntranceIndex].Id;
        var connectors = BuildPassages(layout, rooms, locations, input);
        var frontDoor = new LocationConnector
        {
            OriginLocationId = entranceLocationId,
            Name = "Front Door",
            Description = "The way back outside.",
            DestinationLocationId = input.WildernessLocation.Id,
            DestinationLabel = "Outside",
            WorldId = input.WorldId,
        };
        connectors.Add(frontDoor);
        connectors.Add(
            new LocationConnector
            {
                OriginLocationId = input.WildernessLocation.Id,
                Name = "Entrance",
                Description = $"The way into {name}.",
                DestinationLocationId = entranceLocationId,
                DestinationLabel = name,
                WorldId = input.WorldId,
            }
        );

        return new DungeonGeneratorResult(
            building,
            placements,
            rooms,
            locations,
            connectors,
            new DoorConnector { ConnectorId = frontDoor.Id, WorldId = input.WorldId },
            entranceLocationId,
            locations[layout.BossIndex].Id
        );
    }

    // Passages run both ways: a player who walks into a room has to be able to walk back out of it.
    private static List<LocationConnector> BuildPassages(
        DungeonLayout layout,
        IReadOnlyList<Room> rooms,
        IReadOnlyList<Location> locations,
        DungeonGeneratorInput input
    )
    {
        var connectors = new List<LocationConnector>();

        foreach (var passage in layout.Passages)
        {
            connectors.Add(Passage(rooms, locations, layout, passage.From, passage.To, input));
            connectors.Add(Passage(rooms, locations, layout, passage.To, passage.From, input));
        }

        return connectors;
    }

    private static LocationConnector Passage(
        IReadOnlyList<Room> rooms,
        IReadOnlyList<Location> locations,
        DungeonLayout layout,
        int from,
        int to,
        DungeonGeneratorInput input
    )
    {
        var direction = Bearings.Between(layout.Rooms[from].Position, layout.Rooms[to].Position);
        var lie =
            layout.Rooms[to].DepthFromEntrance > layout.Rooms[from].DepthFromEntrance
                ? "deeper in"
                : "back toward the way you came";

        return new LocationConnector
        {
            OriginLocationId = locations[from].Id,
            Name = "Passage",
            Description = $"A passage running {Bearings.ToWords(direction)}, {lie}.",
            Direction = direction,
            DestinationLocationId = locations[to].Id,
            DestinationLabel = rooms[to].Name,
            WorldId = input.WorldId,
        };
    }

    // Exits are chosen by name, so two rooms sharing one would leave the player unable to say which
    // they meant. A qualifier also gives them something to navigate by, absent any map.
    private static string UniqueName(string name, HashSet<string> used, Random random)
    {
        if (used.Add(name))
        {
            return name;
        }

        var qualifiers = Qualifiers.OrderBy(_ => random.Next()).ToArray();
        foreach (var qualifier in qualifiers)
        {
            var qualified = $"{qualifier} {name}";
            if (used.Add(qualified))
            {
                return qualified;
            }
        }

        var numbered = $"{name} {used.Count}";
        used.Add(numbered);
        return numbered;
    }

    private static (BuildingType Type, string Name) ChooseNamedType(DungeonGeneratorInput input)
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

        return availablePairs[input.Random.Next(availablePairs.Length)];
    }
}
