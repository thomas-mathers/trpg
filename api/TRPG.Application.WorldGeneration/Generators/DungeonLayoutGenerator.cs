using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal enum DungeonRouteKind
{
    None,
    Long,
    Short,
    Third,
}

internal record DungeonLayoutInput(int RoomCount)
{
    public required Random Random { get; init; }
}

internal readonly record struct DungeonPassage(int From, int To);

internal sealed record DungeonRouteRooms(
    IReadOnlyList<int> Long,
    IReadOnlyList<int> Short,
    IReadOnlyList<int> Third
);

internal sealed record DungeonConvergenceFeatures(
    bool IncludeFloorSplit,
    bool IncludeLeverGate,
    bool IncludeShortcutFiller
);

internal record DungeonRoomNode(
    int Index,
    Point Position,
    int DepthFromEntrance,
    bool IsDeadEnd,
    DungeonRouteKind RouteKind,
    int FloorNumber
);

internal record DungeonLayout(
    IReadOnlyList<DungeonRoomNode> Rooms,
    IReadOnlyCollection<DungeonPassage> Passages,
    int EntranceIndex,
    int BossIndex,
    int? LandingIndex,
    bool HasLeverGate,
    int BackDoorIndex
);

// Entrance and boss are fixed endpoints, and every route between them is authored, not
// discovered by scattering rooms and classifying whatever graph comes out. A dungeon always has
// a safe Long route (loot-bearing dead ends only ever branch off this one) and an obstacle-gated
// Short route; rarely, a Third route. Routes are disjoint — they share nothing but the two
// endpoints, so a gate on one route can never be walked around through another route's rooms.
//
// Depth is assigned directly as each route is built, not derived afterward by a graph search:
// every route converges on the boss, so a plain shortest-path search would give the boss (and
// anything reached back through it) the *shortest* route's depth, corrupting the longer routes'
// own rooms with a shallower depth than their real position in the chain.
internal static class DungeonLayoutGenerator
{
    private const int MinimumLongRouteLength = 3;
    private const int MaximumLongRouteLength = 6;
    private const int MinimumShortRouteLength = 1;
    private const int MaximumShortRouteLength = 3;
    private const int MaximumDeadEnds = 3;
    private const int MinimumRoomCountForThirdRoute = 11;
    private const double ThirdRouteChance = 0.2;
    private const double FloorSplitChance = 0.15;
    private const double LeverGateChance = 0.25;
    private const double ShortcutFillerChance = 0.5;
    private const int UpperFloorNumber = 1;
    private const double ShortcutLane = 3 * LaneSpacing;

    public static DungeonLayout Generate(DungeonLayoutInput input)
    {
        var budget = Math.Max(0, input.RoomCount - 2);
        var longLength = Math.Clamp(
            (int)Math.Round(budget * 0.5),
            MinimumLongRouteLength,
            MaximumLongRouteLength
        );
        budget -= longLength;

        var shortLength = Math.Clamp(
            (int)Math.Round(budget * 0.4),
            MinimumShortRouteLength,
            MaximumShortRouteLength
        );
        budget -= shortLength;

        var deadEndCount = Math.Clamp(budget, 0, MaximumDeadEnds);
        budget -= deadEndCount;

        var thirdLength = 0;
        if (
            input.RoomCount >= MinimumRoomCountForThirdRoute
            && input.Random.NextDouble() < ThirdRouteChance
        )
        {
            thirdLength = Math.Clamp(budget, MinimumShortRouteLength, MaximumShortRouteLength);
        }

        var includeFloorSplit = input.Random.NextDouble() < FloorSplitChance;
        var includeLeverGate = input.Random.NextDouble() < LeverGateChance;
        var includeShortcutFiller = input.Random.NextDouble() < ShortcutFillerChance;

        var state = new LayoutBuilder();
        var longRouteRooms = state.BuildRoute(DungeonRouteKind.Long, -LaneSpacing, longLength);
        state.AttachDeadEnds(longRouteRooms, deadEndCount, input.Random);
        var shortRouteRooms = state.BuildRoute(DungeonRouteKind.Short, LaneSpacing, shortLength);
        var thirdRouteRooms =
            thirdLength > 0
                ? state.BuildRoute(DungeonRouteKind.Third, 2 * LaneSpacing, thirdLength)
                : [];

        return state.Finish(
            new DungeonRouteRooms(longRouteRooms, shortRouteRooms, thirdRouteRooms),
            new DungeonConvergenceFeatures(
                includeFloorSplit,
                includeLeverGate,
                includeShortcutFiller
            )
        );
    }

    private const double LaneSpacing = 25;
    private const double RouteSpan = 100;
    private const int Entrance = 0;
    private const int Boss = 1;

    // Holds every dictionary a route needs to mutate while it's being built, so the route-building
    // methods themselves stay under the 5-parameter limit instead of threading five collections
    // through every call.
    private sealed class LayoutBuilder
    {
        private readonly HashSet<DungeonPassage> _passages = [];
        private readonly Dictionary<int, DungeonRouteKind> _routeKindByIndex = new()
        {
            [Entrance] = DungeonRouteKind.None,
            [Boss] = DungeonRouteKind.None,
        };
        private readonly Dictionary<int, double> _laneByIndex = new()
        {
            [Entrance] = 0,
            [Boss] = 0,
        };
        private readonly Dictionary<int, int> _depthByIndex = new() { [Entrance] = 0 };
        private readonly Dictionary<int, int> _floorByIndex = new() { [Entrance] = 0, [Boss] = 0 };
        private readonly HashSet<int> _deadEndIndices = [];
        private int _nextIndex = 2;

        // A route is a chain of fresh rooms from entrance to boss, sharing nothing else with any
        // other route — that disjointness is what stops a gate on one route ever being walked
        // around through another route's rooms. The connection into the boss itself is added by
        // Finish once every route's length is known, so the boss's own depth can be computed once,
        // up front.
        public List<int> BuildRoute(DungeonRouteKind kind, double lane, int length)
        {
            var routeRooms = new List<int>();
            var previous = Entrance;

            for (var step = 0; step < length; step++)
            {
                var room = _nextIndex++;
                routeRooms.Add(room);
                _routeKindByIndex[room] = kind;
                _laneByIndex[room] = lane;
                _depthByIndex[room] = _depthByIndex[previous] + 1;
                _floorByIndex[room] = 0;
                _passages.Add(Normalize(previous, room));
                previous = room;
            }

            return routeRooms;
        }

        // Dead ends only ever branch off the Long route — the safe route is where exploring off
        // the beaten path is worth the walk, not a route that's already gated by its own obstacle.
        public void AttachDeadEnds(IReadOnlyList<int> longRouteRooms, int count, Random random)
        {
            if (longRouteRooms.Count == 0)
            {
                return;
            }

            // Nothing stops two dead ends from sharing a parent — when that happens, offsetting by
            // a fixed amount would give them the exact same position, so each additional leaf off
            // the same parent gets pushed further out instead.
            var attachedCountByParent = new Dictionary<int, int>();

            for (var deadEndIndex = 0; deadEndIndex < count; deadEndIndex++)
            {
                var parent = longRouteRooms[random.Next(longRouteRooms.Count)];
                var siblingCount = attachedCountByParent.GetValueOrDefault(parent);
                attachedCountByParent[parent] = siblingCount + 1;

                var leaf = _nextIndex++;
                _routeKindByIndex[leaf] = DungeonRouteKind.Long;
                _laneByIndex[leaf] = _laneByIndex[parent] - LaneSpacing / 2 * (siblingCount + 1);
                _depthByIndex[leaf] = _depthByIndex[parent] + 1;
                _floorByIndex[leaf] = 0;
                _deadEndIndices.Add(leaf);
                _passages.Add(Normalize(parent, leaf));
            }
        }

        public DungeonLayout Finish(DungeonRouteRooms routes, DungeonConvergenceFeatures features)
        {
            // Whatever room every route converges into next has to read as deeper than each
            // route's own last room, not just the nearest one, since a player can arrive there
            // from any of them.
            var convergenceDepth =
                1
                + new[]
                {
                    LastDepth(routes.Long),
                    LastDepth(routes.Short),
                    LastDepth(routes.Third),
                }.Max();

            // A floor split and a mandatory lever gate both need every route to funnel through one
            // shared room before the boss — the same convergence-point construct either way, so it
            // is built once here and only the staircase-flavored floor bump is conditional on which
            // of the two (or both) actually rolled.
            var needsConvergenceRoom = features.IncludeFloorSplit || features.IncludeLeverGate;

            int? landing = null;
            if (needsConvergenceRoom)
            {
                landing = _nextIndex++;
                _routeKindByIndex[landing.Value] = DungeonRouteKind.None;
                _laneByIndex[landing.Value] = 0;
                _depthByIndex[landing.Value] = convergenceDepth;
                _floorByIndex[landing.Value] = 0;

                _passages.Add(Normalize(LastRoomOf(routes.Long), landing.Value));
                _passages.Add(Normalize(LastRoomOf(routes.Short), landing.Value));
                if (routes.Third.Count > 0)
                {
                    _passages.Add(Normalize(LastRoomOf(routes.Third), landing.Value));
                }

                _passages.Add(Normalize(landing.Value, Boss));
                _depthByIndex[Boss] = convergenceDepth + 1;
                _floorByIndex[Boss] = features.IncludeFloorSplit ? UpperFloorNumber : 0;
            }
            else
            {
                _depthByIndex[Boss] = convergenceDepth;
                _passages.Add(Normalize(LastRoomOf(routes.Long), Boss));
                _passages.Add(Normalize(LastRoomOf(routes.Short), Boss));
                if (routes.Third.Count > 0)
                {
                    _passages.Add(Normalize(LastRoomOf(routes.Third), Boss));
                }
            }

            var backDoor = BuildMandatoryShortcut(features.IncludeShortcutFiller);

            var maxDepth = Math.Max(1, _depthByIndex[Boss]);
            var rooms = Enumerable
                .Range(0, _nextIndex)
                .Select(index => new DungeonRoomNode(
                    index,
                    new Point(
                        RouteSpan * ((double)_depthByIndex[index] / maxDepth),
                        _laneByIndex[index]
                    ),
                    _depthByIndex[index],
                    _deadEndIndices.Contains(index),
                    _routeKindByIndex[index],
                    _floorByIndex[index]
                ))
                .ToArray();

            return new DungeonLayout(
                rooms,
                _passages,
                Entrance,
                Boss,
                landing,
                features.IncludeLeverGate,
                backDoor
            );
        }

        // Every dungeon gets a shortcut back near the entrance once the boss is reached — free to
        // walk from the boss side, locked from the entrance side until its lever (placed in the
        // boss room) is pulled. An occasional filler room between the boss and the back door keeps
        // the shortcut from reading as an implausibly direct tunnel every single time.
        private int BuildMandatoryShortcut(bool includeFiller)
        {
            var backDoor = _nextIndex++;
            _routeKindByIndex[backDoor] = DungeonRouteKind.None;
            _laneByIndex[backDoor] = ShortcutLane;
            _depthByIndex[backDoor] = 1;
            _floorByIndex[backDoor] = 0;

            if (includeFiller)
            {
                var filler = _nextIndex++;
                _routeKindByIndex[filler] = DungeonRouteKind.None;
                _laneByIndex[filler] = ShortcutLane;
                _depthByIndex[filler] = 2;
                _floorByIndex[filler] = 0;
                _passages.Add(Normalize(Boss, filler));
                _passages.Add(Normalize(filler, backDoor));
            }
            else
            {
                _passages.Add(Normalize(Boss, backDoor));
            }

            _passages.Add(Normalize(Entrance, backDoor));

            return backDoor;
        }

        private int LastDepth(IReadOnlyList<int> routeRooms) =>
            routeRooms.Count == 0 ? 0 : _depthByIndex[routeRooms[^1]];

        private static int LastRoomOf(IReadOnlyList<int> routeRooms) =>
            routeRooms.Count == 0 ? Entrance : routeRooms[^1];

        private static DungeonPassage Normalize(int from, int to) =>
            from < to ? new DungeonPassage(from, to) : new DungeonPassage(to, from);
    }
}
