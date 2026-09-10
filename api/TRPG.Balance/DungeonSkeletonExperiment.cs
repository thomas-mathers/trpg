using System.Text;

namespace TRPG.Balance;

// Prototype only — validates the skeleton-first approach (lock in entrance/boss/routes/shortcut,
// then randomize the rest) before any of it lands in TRPG.Application.WorldGeneration.
internal static class DungeonSkeletonExperiment
{
    private static readonly string[] GateTypes = ["Combat", "Lock", "Key", "Social"];

    public static void Run(
        int routeCount,
        int minRouteLength,
        int maxRouteLength,
        int maxShortcutFillers,
        int decorativeDeadEnds,
        int layoutCount,
        int seed,
        string outputPath
    )
    {
        var random = new Random(seed);
        using var writer = new StreamWriter(outputPath);

        for (var layoutIndex = 0; layoutIndex < layoutCount; layoutIndex++)
        {
            var skeleton = Generate(
                routeCount,
                minRouteLength,
                maxRouteLength,
                maxShortcutFillers,
                decorativeDeadEnds,
                random
            );

            Console.WriteLine(
                $"layout {layoutIndex}: rooms={skeleton.RoomCount} "
                    + $"routeLengths=[{string.Join(",", skeleton.RouteLengths)}] "
                    + $"shortcutHops={skeleton.ShortcutHops} "
                    + $"deadEnds={decorativeDeadEnds}"
            );

            writer.WriteLine($"## Skeleton {layoutIndex}");
            writer.WriteLine();
            writer.WriteLine("```mermaid");
            writer.Write(skeleton.Mermaid);
            writer.WriteLine("```");
            writer.WriteLine();
        }

        Console.WriteLine($"Wrote {layoutCount} skeleton(s) to {outputPath}");
    }

    private record Skeleton(
        int RoomCount,
        IReadOnlyList<int> RouteLengths,
        int ShortcutHops,
        string Mermaid
    );

    private static Skeleton Generate(
        int routeCount,
        int minRouteLength,
        int maxRouteLength,
        int maxShortcutFillers,
        int decorativeDeadEnds,
        Random random
    )
    {
        const int entrance = 0;
        const int boss = 1;
        var nextIndex = 2;

        var mermaid = new StringBuilder("graph LR\n");
        mermaid.AppendLine($"    {entrance}[\"Entrance\"]");
        mermaid.AppendLine($"    {boss}[\"Boss\"]");

        var routeLengths = new List<int>();
        var routeRooms = new List<int>();

        for (var routeIndex = 0; routeIndex < routeCount; routeIndex++)
        {
            var gateType = GateTypes[routeIndex % GateTypes.Length];
            var length = random.Next(minRouteLength, maxRouteLength + 1);
            routeLengths.Add(length);

            var previous = entrance;
            for (var step = 0; step < length; step++)
            {
                var room = nextIndex++;
                routeRooms.Add(room);
                mermaid.AppendLine($"    {room}[\"R{routeIndex} #{step}\"]");

                if (step == 0)
                {
                    mermaid.AppendLine($"    {previous} -->|{gateType}| {room}");
                }
                else
                {
                    mermaid.AppendLine($"    {previous} --- {room}");
                }

                previous = room;
            }

            mermaid.AppendLine($"    {previous} --- {boss}");
        }

        // The shortcut: boss to a small number of filler rooms to a back door adjacent to the
        // entrance. Free to walk boss-to-entrance; the entrance-side approach is locked until a
        // lever near the boss is pulled — same "only the entry side is lockable" idiom as
        // BuildingGenerator's front doors.
        var fillerCount = random.Next(0, maxShortcutFillers + 1);
        var shortcutPrevious = boss;
        for (var fillerIndex = 0; fillerIndex < fillerCount; fillerIndex++)
        {
            var filler = nextIndex++;
            mermaid.AppendLine($"    {filler}[\"Shortcut filler\"]");
            mermaid.AppendLine($"    {shortcutPrevious} --- {filler}");
            shortcutPrevious = filler;
        }

        var backDoor = nextIndex++;
        mermaid.AppendLine($"    {backDoor}[\"Back door\"]");
        mermaid.AppendLine($"    {shortcutPrevious} --- {backDoor}");
        mermaid.AppendLine($"    {backDoor} -.->|lever, locked from here| {entrance}");
        var shortcutHops = fillerCount + 2;

        // Decorative dead ends, hung off route rooms only — never off entrance, boss, or the
        // shortcut chain, so they can never blur which edges are load-bearing.
        for (
            var deadEndIndex = 0;
            deadEndIndex < decorativeDeadEnds && routeRooms.Count > 0;
            deadEndIndex++
        )
        {
            var parent = routeRooms[random.Next(routeRooms.Count)];
            var leaf = nextIndex++;
            mermaid.AppendLine($"    {leaf}[\"Dead end\"]");
            mermaid.AppendLine($"    {parent} --- {leaf}");
        }

        return new Skeleton(nextIndex, routeLengths, shortcutHops, mermaid.ToString());
    }
}
