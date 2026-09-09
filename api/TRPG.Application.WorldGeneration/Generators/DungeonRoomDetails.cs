using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

// A role has only a handful of base lines, so a large dungeon reuses them. Pairing each with a
// detail turns three descriptions into two dozen, and details are the concrete things a narrator
// can pick up and run with.
internal static class DungeonRoomDetails
{
    private static readonly string[] Common =
    [
        "A cold firepit sits in the middle, ringed with stones.",
        "Tally marks are scratched beside the doorway, dozens of them.",
        "Bones have been swept into one corner, none of them recent.",
        "A cart lies on its side, one wheel missing.",
        "Something has clawed long grooves down the far wall.",
        "The air moves here, though it is not clear from where.",
        "Water finds its way in and runs along one wall in a thin line.",
        "A lantern hangs on a hook, its oil long dry.",
    ];

    private static readonly Dictionary<BuildingType, string[]> ByDungeonType = new()
    {
        [BuildingType.Cave] =
        [
            "Roots have pushed through the ceiling and hang at head height.",
            "The floor is uneven enough to turn an ankle.",
            "Something small scatters away from the light.",
        ],
        [BuildingType.Crypt] =
        [
            "Names are carved into the walls in a script nobody uses now.",
            "A niche stands empty, its slab pried off and leaning.",
            "Dust lies thick and undisturbed, except where it is not.",
        ],
        [BuildingType.Mine] =
        [
            "Rails run through and stop where the roof came down.",
            "Pick marks cover the walls in every direction.",
            "Timbers hold up the ceiling, and some of them are bowed.",
        ],
        [BuildingType.Ruins] =
        [
            "Half a mosaic survives underfoot, the rest scattered.",
            "A doorway has been bricked up, badly, and recently.",
            "Roof beams lie where they fell, blackened at one end.",
        ],
        [BuildingType.Tower] =
        [
            "A window slit looks out on nothing but sky.",
            "The floor slopes, a little, toward the outer wall.",
            "Chalk diagrams cover one wall, rain-smeared into nonsense.",
        ],
    };

    public static string For(BuildingType dungeonType, Random random)
    {
        var specific = ByDungeonType.GetValueOrDefault(dungeonType, []);
        var options = specific.Concat(Common).ToArray();

        return options[random.Next(options.Length)];
    }
}
