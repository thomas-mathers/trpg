using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DungeonRoomContent(string Name, string Description);

// A dungeon is read, not looked at, so what a room is called and how it reads is the whole of its
// character. Roles carry that, and which roles a type can draw is what makes a mine unlike a crypt.
internal static class DungeonRoomCatalog
{
    // A dead end nobody had a reason to walk to teaches the player to stop exploring, so only roles
    // that pay may sit at one.
    public static readonly IReadOnlyCollection<RoomRole> PayingRoles =
    [
        RoomRole.TreasureRoom,
        RoomRole.Shrine,
        RoomRole.Study,
        RoomRole.CellBlock,
        RoomRole.Storeroom,
    ];

    private static readonly Dictionary<BuildingType, RoomRole[]> RolesByDungeonType = new()
    {
        [BuildingType.Cave] =
        [
            RoomRole.Passage,
            RoomRole.FloodedSump,
            RoomRole.CollapsedGallery,
            RoomRole.Storeroom,
            RoomRole.Shrine,
        ],
        [BuildingType.Crypt] =
        [
            RoomRole.Passage,
            RoomRole.Shrine,
            RoomRole.CellBlock,
            RoomRole.TreasureRoom,
            RoomRole.Study,
        ],
        [BuildingType.Mine] =
        [
            RoomRole.Passage,
            RoomRole.CollapsedGallery,
            RoomRole.FloodedSump,
            RoomRole.Storeroom,
            RoomRole.GuardPost,
        ],
        [BuildingType.Ruins] =
        [
            RoomRole.Passage,
            RoomRole.GuardPost,
            RoomRole.Study,
            RoomRole.CellBlock,
            RoomRole.Shrine,
        ],
        [BuildingType.Tower] =
        [
            RoomRole.Passage,
            RoomRole.Study,
            RoomRole.GuardPost,
            RoomRole.TreasureRoom,
            RoomRole.Shrine,
        ],
    };

    private static readonly Dictionary<RoomRole, DungeonRoomContent[]> ContentByRole = new()
    {
        [RoomRole.Entrance] =
        [
            new("Threshold", "Daylight reaches a little way in and then gives up."),
            new("Mouth", "The way back out is still in sight, which is its own comfort."),
        ],
        [RoomRole.BossChamber] =
        [
            new("Great Chamber", "The ceiling opens up well past the reach of any lamp."),
            new("Inner Sanctum", "Everything here was arranged by someone, and recently."),
        ],
        [RoomRole.Passage] =
        [
            new("Crooked Passage", "The walls close in and open out again without warning."),
            new("Long Gallery", "Footsteps carry further here than feels wise."),
            new("Junction", "Ways lead off in more directions than you would like."),
        ],
        [RoomRole.GuardPost] =
        [
            new("Watch Post", "Someone sat here long enough to wear the stone smooth."),
            new("Barricade", "Furniture has been dragged into a rough wall across the way."),
        ],
        [RoomRole.Storeroom] =
        [
            new("Storeroom", "Crates and sacks, most of them split and long since spoiled."),
            new("Supply Cache", "Whatever was worth taking has mostly been taken."),
        ],
        [RoomRole.TreasureRoom] =
        [
            new("Strongroom", "The door was the expensive part, and it held until now."),
            new("Hoard", "Someone gathered all this and never came back for it."),
        ],
        [RoomRole.Shrine] =
        [
            new("Shrine", "Offerings have been left here, some of them recently."),
            new("Reliquary", "A niche in the wall holds something wrapped in cloth."),
        ],
        [RoomRole.Study] =
        [
            new("Study", "Shelves sag under damp paper nobody has read in years."),
            new("Scriptorium", "A writing desk, ink long dried into the grain."),
        ],
        [RoomRole.CellBlock] =
        [
            new("Cells", "Iron doors stand in a row, most of them open."),
            new("Oubliette", "The only way down was never meant to be a way back up."),
        ],
        [RoomRole.CollapsedGallery] =
        [
            new("Collapsed Gallery", "Half the roof is on the floor and the rest looks unsure."),
            new("Fallen Span", "Rubble blocks what was clearly once a wider way through."),
        ],
        [RoomRole.FloodedSump] =
        [
            new("Flooded Sump", "Standing water reaches your shins and hides the floor."),
            new("Seep", "Water runs down the walls and pools in the low corner."),
        ],
    };

    public static IReadOnlyCollection<RoomRole> RolesFor(BuildingType dungeonType) =>
        RolesByDungeonType.TryGetValue(dungeonType, out var roles) ? roles : [RoomRole.Passage];

    public static DungeonRoomContent ContentFor(RoomRole role, Random random)
    {
        var options = ContentByRole[role];

        return options[random.Next(options.Length)];
    }
}
