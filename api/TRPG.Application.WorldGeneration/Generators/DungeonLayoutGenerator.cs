using SharpVoronoiLib;
using TRPG.Application.Common.Algorithms;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DungeonLayoutInput(int RoomCount, double LoopEdgeFraction = 0.15)
{
    public required Random Random { get; init; }
}

internal readonly record struct DungeonPassage(int From, int To);

internal record DungeonRoomNode(int Index, Point Position, int DepthFromEntrance, bool IsDeadEnd);

internal record DungeonLayout(
    IReadOnlyList<DungeonRoomNode> Rooms,
    IReadOnlyCollection<DungeonPassage> Passages,
    int EntranceIndex,
    int BossIndex
);

// A spanning tree guarantees every room is reachable, which is the property that stops a dungeon
// soft-locking. Putting a few of the discarded edges back is what stops it being a bare tree the
// player can only ever retrace.
internal static class DungeonLayoutGenerator
{
    private const double MinimumSeparation = 10;
    private const double ScatterSpan = 100;
    private const int PlacementAttempts = 64;
    private const double DeadEndFraction = 0.3;
    private const int MinimumDeadEnds = 2;

    public static DungeonLayout Generate(DungeonLayoutInput input)
    {
        var positions = ScatterRooms(input.RoomCount, input.Random);
        var candidates = NeighbouringRooms(positions);
        var passages = SelectPassages(positions, candidates, input);

        var neighbours = BuildAdjacency(positions.Count, passages);
        var (entrance, boss) = ChooseEndpoints(positions.Count, neighbours);
        var depths = Depths(entrance, neighbours);

        // The boss chamber is terminal too, but it already pays, so it is not somewhere that needs
        // filling with a reason to have walked there.
        var rooms = positions
            .Select(
                (position, index) =>
                    new DungeonRoomNode(
                        index,
                        position,
                        depths[index],
                        neighbours[index].Count == 1 && index != entrance && index != boss
                    )
            )
            .ToArray();

        return new DungeonLayout(rooms, passages, entrance, boss);
    }

    // Rooms whose Voronoi cells touch are the ones worth running a passage between: it keeps the
    // layout planar, so no two passages cross on a map, and stops a spanning tree linking opposite
    // ends of the dungeon.
    private static IReadOnlyCollection<DungeonPassage> NeighbouringRooms(
        IReadOnlyList<Point> positions
    )
    {
        if (positions.Count < 2)
        {
            return [];
        }

        var sites = positions.Select(position => new VoronoiSite(position.X, position.Y)).ToList();
        var plane = new VoronoiPlane(0, 0, ScatterSpan, ScatterSpan);
        plane.SetSites(sites);
        plane.Tessellate();

        var indexBySite = new Dictionary<VoronoiSite, int>();
        for (var index = 0; index < sites.Count; index++)
        {
            indexBySite[sites[index]] = index;
        }

        var passages = new HashSet<DungeonPassage>();
        foreach (var (site, index) in indexBySite)
        {
            foreach (var neighbour in site.Neighbours)
            {
                passages.Add(Normalize(index, indexBySite[neighbour]));
            }
        }

        return passages;
    }

    // Rejection sampling rather than separation steering: with a dozen rooms there is no packing
    // problem to solve, only a need for points that are not on top of each other.
    private static List<Point> ScatterRooms(int roomCount, Random random)
    {
        var positions = new List<Point>();

        while (positions.Count < roomCount)
        {
            var placed = false;
            for (var attempt = 0; attempt < PlacementAttempts && !placed; attempt++)
            {
                var candidate = new Point(
                    random.NextDouble() * ScatterSpan,
                    random.NextDouble() * ScatterSpan
                );
                if (positions.All(other => Distance(candidate, other) >= MinimumSeparation))
                {
                    positions.Add(candidate);
                    placed = true;
                }
            }

            if (!placed)
            {
                break;
            }
        }

        return positions;
    }

    private static IReadOnlyCollection<DungeonPassage> SelectPassages(
        IReadOnlyList<Point> positions,
        IReadOnlyCollection<DungeonPassage> candidates,
        DungeonLayoutInput input
    )
    {
        var adjacency = BuildAdjacency(positions.Count, candidates);

        var spanning = Graphs
            .MinimumSpanningTree(
                0,
                node => adjacency[node],
                (from, to) => Distance(positions[from], positions[to])
            )
            .Select(edge => Normalize(edge.From, edge.To))
            .ToHashSet();

        var protectedRooms = ReserveDeadEnds(positions.Count, spanning, input);
        var discarded = candidates
            .Where(edge => !spanning.Contains(Normalize(edge.From, edge.To)))
            .ToArray();

        // A share of what the tree threw away, per the algorithm this follows, minus anything that
        // would connect a room being kept as a dead end.
        var loopCount = (int)Math.Round(discarded.Length * input.LoopEdgeFraction);
        var loops = discarded
            .Where(edge => !protectedRooms.Contains(edge.From) && !protectedRooms.Contains(edge.To))
            .OrderBy(_ => input.Random.Next())
            .Take(loopCount);

        return [.. spanning, .. loops];
    }

    // Dead ends are where exploring has to pay, so they are chosen rather than left to survive by
    // chance: a single loop edge landing on a leaf is enough to stop it being one.
    private static HashSet<int> ReserveDeadEnds(
        int roomCount,
        IReadOnlyCollection<DungeonPassage> spanning,
        DungeonLayoutInput input
    )
    {
        var adjacency = BuildAdjacency(roomCount, spanning);
        var leaves = Enumerable
            .Range(0, roomCount)
            .Where(room => adjacency[room].Count == 1)
            .OrderBy(_ => input.Random.Next())
            .ToArray();

        var target = Math.Max(MinimumDeadEnds, (int)Math.Round(roomCount * DeadEndFraction));

        return leaves.Take(Math.Min(target, leaves.Length)).ToHashSet();
    }

    // The two rooms furthest apart through the passages, so the way in and the way to the boss are
    // never neighbours and the dungeon has a length to it.
    private static (int Entrance, int Boss) ChooseEndpoints(
        int roomCount,
        IReadOnlyList<List<int>> neighbours
    )
    {
        var furthestFromFirst = Furthest(0, neighbours);
        var boss = Furthest(furthestFromFirst, neighbours);

        return roomCount < 2 ? (0, 0) : (furthestFromFirst, boss);
    }

    private static int Furthest(int origin, IReadOnlyList<List<int>> neighbours)
    {
        var depths = Depths(origin, neighbours);
        var furthest = origin;

        for (var index = 0; index < depths.Length; index++)
        {
            if (depths[index] > depths[furthest])
            {
                furthest = index;
            }
        }

        return furthest;
    }

    private static int[] Depths(int origin, IReadOnlyList<List<int>> neighbours)
    {
        var depths = new int[neighbours.Count];
        Array.Fill(depths, -1);
        depths[origin] = 0;

        var queue = new Queue<int>();
        queue.Enqueue(origin);

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

        return depths;
    }

    private static List<List<int>> BuildAdjacency(
        int roomCount,
        IReadOnlyCollection<DungeonPassage> edges
    )
    {
        var adjacency = Enumerable.Range(0, roomCount).Select(_ => new List<int>()).ToList();

        foreach (var edge in edges)
        {
            adjacency[edge.From].Add(edge.To);
            adjacency[edge.To].Add(edge.From);
        }

        return adjacency;
    }

    private static DungeonPassage Normalize(int from, int to) =>
        from < to ? new DungeonPassage(from, to) : new DungeonPassage(to, from);

    private static double Distance(Point left, Point right) =>
        Math.Sqrt(Math.Pow(left.X - right.X, 2) + Math.Pow(left.Y - right.Y, 2));
}
