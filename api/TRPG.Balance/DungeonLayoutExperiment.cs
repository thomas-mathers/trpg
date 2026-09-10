using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Balance;

internal static class DungeonLayoutExperiment
{
    public static void Run(int roomCount, int layoutCount, int seed, string outputPath)
    {
        var random = new Random(seed);
        using var writer = new StreamWriter(outputPath);

        for (var layoutIndex = 0; layoutIndex < layoutCount; layoutIndex++)
        {
            var layout = DungeonLayoutGenerator.Generate(
                new DungeonLayoutInput(roomCount) { Random = random }
            );

            var bossDistanceFromEntrance = DistanceBetween(
                layout,
                layout.EntranceIndex,
                layout.BossIndex
            );
            var deadEndCount = layout.Rooms.Count(room => room.IsDeadEnd);

            Console.WriteLine(
                $"layout {layoutIndex}: rooms={layout.Rooms.Count} bossDistance={bossDistanceFromEntrance} deadEnds={deadEndCount}"
            );

            writer.WriteLine($"## Layout {layoutIndex}");
            writer.WriteLine();
            writer.WriteLine("```mermaid");
            writer.Write(DungeonLayoutDiagram.ToMermaid(layout));
            writer.WriteLine("```");
            writer.WriteLine();
        }

        Console.WriteLine($"Wrote {layoutCount} layout(s) to {outputPath}");
    }

    private static int DistanceBetween(DungeonLayout layout, int from, int to)
    {
        var neighbours = layout.Rooms.Select(_ => new List<int>()).ToArray();
        foreach (var passage in layout.Passages)
        {
            neighbours[passage.From].Add(passage.To);
            neighbours[passage.To].Add(passage.From);
        }

        var depths = new int[layout.Rooms.Count];
        Array.Fill(depths, -1);
        depths[from] = 0;

        var queue = new Queue<int>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var next in neighbours[node])
            {
                if (depths[next] != -1)
                {
                    continue;
                }

                depths[next] = depths[node] + 1;
                queue.Enqueue(next);
            }
        }

        return depths[to];
    }
}
