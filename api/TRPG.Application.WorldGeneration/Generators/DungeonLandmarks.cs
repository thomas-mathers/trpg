using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

// With no map, a player navigates by memory of places. A handful of rooms per dungeon get something
// singular so there is something to steer by; every room having one would mean none of them do.
internal static class DungeonLandmarks
{
    public const int PerDungeon = 3;

    private static readonly Dictionary<BuildingType, string[]> ByDungeonType = new()
    {
        [BuildingType.Cave] =
        [
            "A chasm splits the floor, crossed by a rope bridge that has seen better years.",
            "A column of pale stone runs floor to ceiling, worn smooth by hands.",
            "An underground pool lies black and perfectly still.",
        ],
        [BuildingType.Crypt] =
        [
            "A statue stands with its face chiselled away, deliberately and long ago.",
            "Sarcophagi are stacked three high along both walls.",
            "A vast bronze door, sealed, with no handle on this side.",
        ],
        [BuildingType.Mine] =
        [
            "A cage lift hangs in its shaft, counterweight resting on the floor.",
            "A seam of something bright runs through the rock, half cut away.",
            "A waterwheel stands stopped in a channel that no longer runs.",
        ],
        [BuildingType.Ruins] =
        [
            "A mural covers one wall: a city, and a procession leaving it.",
            "Two vast feet stand on a plinth, the rest of the statue gone.",
            "A courtyard open to the sky, with a dead tree still rooted in it.",
        ],
        [BuildingType.Tower] =
        [
            "An orrery hangs from the ceiling, half its arms snapped off.",
            "A spiral stair winds up past this floor and is blocked above.",
            "A mirror the height of a person, backing flaked to patches.",
        ],
    };

    public static string? For(BuildingType dungeonType, Random random)
    {
        var options = ByDungeonType.GetValueOrDefault(dungeonType, []);

        return options.Length == 0 ? null : options[random.Next(options.Length)];
    }
}
