using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal enum DungeonRoomRole
{
    Entrance,
    BossChamber,
    Passage,
    GuardPost,
    Storeroom,
    TreasureRoom,
    Shrine,
    Study,
    CellBlock,
    CollapsedGallery,
    FloodedSump,
}

internal record DungeonRoomContent(string Name, string Description);

// A dungeon is read, not looked at, so what a room is called and how it reads is the whole of its
// character. Roles carry that, and which roles a type can draw is what makes a mine unlike a crypt.
internal static class DungeonRoomCatalog
{
    // A dead end nobody had a reason to walk to teaches the player to stop exploring, so only roles
    // that pay may sit at one.
    public static readonly IReadOnlyCollection<DungeonRoomRole> PayingRoles =
    [
        DungeonRoomRole.TreasureRoom,
        DungeonRoomRole.Shrine,
        DungeonRoomRole.Study,
        DungeonRoomRole.CellBlock,
        DungeonRoomRole.Storeroom,
    ];

    private static readonly Dictionary<BuildingType, DungeonRoomRole[]> RolesByDungeonType = new()
    {
        [BuildingType.Cave] =
        [
            DungeonRoomRole.Passage,
            DungeonRoomRole.FloodedSump,
            DungeonRoomRole.CollapsedGallery,
            DungeonRoomRole.Storeroom,
            DungeonRoomRole.Shrine,
        ],
        [BuildingType.Crypt] =
        [
            DungeonRoomRole.Passage,
            DungeonRoomRole.Shrine,
            DungeonRoomRole.CellBlock,
            DungeonRoomRole.TreasureRoom,
            DungeonRoomRole.Study,
        ],
        [BuildingType.Mine] =
        [
            DungeonRoomRole.Passage,
            DungeonRoomRole.CollapsedGallery,
            DungeonRoomRole.FloodedSump,
            DungeonRoomRole.Storeroom,
            DungeonRoomRole.GuardPost,
        ],
        [BuildingType.Ruins] =
        [
            DungeonRoomRole.Passage,
            DungeonRoomRole.GuardPost,
            DungeonRoomRole.Study,
            DungeonRoomRole.CellBlock,
            DungeonRoomRole.Shrine,
        ],
        [BuildingType.Tower] =
        [
            DungeonRoomRole.Passage,
            DungeonRoomRole.Study,
            DungeonRoomRole.GuardPost,
            DungeonRoomRole.TreasureRoom,
            DungeonRoomRole.Shrine,
        ],
    };

    private static readonly Dictionary<DungeonRoomRole, DungeonRoomContent[]> ContentByRole = new()
    {
        [DungeonRoomRole.Entrance] =
        [
            new("Threshold", "Daylight reaches a little way in and then gives up."),
            new("Mouth", "The way back out is still in sight, which is its own comfort."),
        ],
        [DungeonRoomRole.BossChamber] =
        [
            new("Great Chamber", "The ceiling opens up well past the reach of any lamp."),
            new("Inner Sanctum", "Everything here was arranged by someone, and recently."),
        ],
        [DungeonRoomRole.Passage] =
        [
            new("Crooked Passage", "The walls close in and open out again without warning."),
            new("Long Gallery", "Footsteps carry further here than feels wise."),
            new("Junction", "Ways lead off in more directions than you would like."),
        ],
        [DungeonRoomRole.GuardPost] =
        [
            new("Watch Post", "Someone sat here long enough to wear the stone smooth."),
            new("Barricade", "Furniture has been dragged into a rough wall across the way."),
        ],
        [DungeonRoomRole.Storeroom] =
        [
            new("Storeroom", "Crates and sacks, most of them split and long since spoiled."),
            new("Supply Cache", "Whatever was worth taking has mostly been taken."),
        ],
        [DungeonRoomRole.TreasureRoom] =
        [
            new("Strongroom", "The door was the expensive part, and it held until now."),
            new("Hoard", "Someone gathered all this and never came back for it."),
        ],
        [DungeonRoomRole.Shrine] =
        [
            new("Shrine", "Offerings have been left here, some of them recently."),
            new("Reliquary", "A niche in the wall holds something wrapped in cloth."),
        ],
        [DungeonRoomRole.Study] =
        [
            new("Study", "Shelves sag under damp paper nobody has read in years."),
            new("Scriptorium", "A writing desk, ink long dried into the grain."),
        ],
        [DungeonRoomRole.CellBlock] =
        [
            new("Cells", "Iron doors stand in a row, most of them open."),
            new("Oubliette", "The only way down was never meant to be a way back up."),
        ],
        [DungeonRoomRole.CollapsedGallery] =
        [
            new("Collapsed Gallery", "Half the roof is on the floor and the rest looks unsure."),
            new("Fallen Span", "Rubble blocks what was clearly once a wider way through."),
        ],
        [DungeonRoomRole.FloodedSump] =
        [
            new("Flooded Sump", "Standing water reaches your shins and hides the floor."),
            new("Seep", "Water runs down the walls and pools in the low corner."),
        ],
    };

    public static IReadOnlyCollection<DungeonRoomRole> RolesFor(BuildingType dungeonType) =>
        RolesByDungeonType.TryGetValue(dungeonType, out var roles)
            ? roles
            : [DungeonRoomRole.Passage];

    public static DungeonRoomContent ContentFor(DungeonRoomRole role, Random random)
    {
        var options = ContentByRole[role];

        return options[random.Next(options.Length)];
    }
}
