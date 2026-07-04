using TRPG.Models;

namespace TRPG.Generators;

internal record DungeonGeneratorInput(Guid StateId, IReadOnlyCollection<string> ExcludedNames, Guid WorldId);

internal record DungeonGeneratorResult(
    Building Building,
    Room Room
);

internal static class DungeonGenerator {
    private static readonly IReadOnlyList<BuildingType> DungeonBuildingTypes =
        BuildingGenerator.DungeonBuildingTypes.ToArray();

    private static readonly Dictionary<BuildingType, string[]> Names = new() {
        [BuildingType.Cave] = [
            "The Dark Hollow", "The Mossy Grotto", "The Black Maw", "The Dripping Cave",
            "The Stone Throat", "The Sunken Hollow", "The Narrow Den",
            "The Cold Cavern", "The Echoing Pit", "The Damp Recess"
        ],
        [BuildingType.Crypt] = [
            "The Sunken Crypt", "The Bone Vault", "The Silent Tomb", "The Sealed Burial",
            "The Forgotten Grave", "The Shroud Chamber", "The Pale Vault",
            "The Warding Tomb", "The Cold Rest", "The Hollow Mausoleum"
        ],
        [BuildingType.Mine] = [
            "The Old Shaft", "The Copper Vein", "The Abandoned Dig", "The Iron Seam",
            "The Broken Pickaxe", "The Salt Works", "The Deep Cut",
            "The Collapsed Tunnel", "The Ore Run", "The Grim Vein"
        ],
        [BuildingType.Ruins] = [
            "The Broken Hall", "The Fallen Spire", "The Shattered Court", "The Crumbled Keep",
            "The Ash Walls", "The Hollow Foundation", "The Rotted Estate",
            "The Scorched Shell", "The Sundered Gate", "The Old Rubble"
        ],
        [BuildingType.Tower] = [
            "The Crumbling Tower", "The Forsaken Spire", "The Watching Pillar", "The Leaning Turret",
            "The Black Spire", "The Warped Tower", "The Silent Watchtower",
            "The Broken Pinnacle", "The Pale Obelisk", "The Hollow Spire"
        ]
    };

    private static readonly Dictionary<BuildingType, string> RoomNames = new() {
        [BuildingType.Cave] = "Cave Entrance",
        [BuildingType.Crypt] = "Burial Chamber",
        [BuildingType.Mine] = "Mine Shaft",
        [BuildingType.Ruins] = "Ruined Hall",
        [BuildingType.Tower] = "Ground Floor"
    };

    public static DungeonGeneratorResult Generate(DungeonGeneratorInput input) {
        var availablePairs = DungeonBuildingTypes
            .SelectMany(type => Names[type]
                .Where(name => !input.ExcludedNames.Contains(name))
                .Select(name => (Type: type, Name: name)))
            .ToArray();

        var (type, name) = availablePairs[Random.Shared.Next(availablePairs.Length)];

        var building = new Building {
            StateId = input.StateId,
            BuildingType = type,
            Name = name,
            WorldId = input.WorldId
        };
        var room = new Room {
            BuildingId = building.Id,
            Name = RoomNames[type],
            Description = "",
            FloorNumber = 0,
            WorldId = input.WorldId
        };
        return new DungeonGeneratorResult(building, room);
    }
}