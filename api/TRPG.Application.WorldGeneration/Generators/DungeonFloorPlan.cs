using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal sealed class DungeonFloorPlan(
    DungeonLayout layout,
    IReadOnlyDictionary<int, Rectangle> bounds
)
{
    private const int RoomGap = 12;
    private const int BranchSpacing = 44;
    private const int LongLane = -48;
    private int ShortLane => HasThirdRoute(layout) ? 48 : 0;
    private int ReturnLane => ShortLane + 40;

    public Rectangle BoundsFor(int index) => bounds[index];

    public static DungeonFloorPlan Create(
        DungeonLayout layout,
        IReadOnlyList<AssignedDungeonRoom> rooms,
        Random random
    )
    {
        var dimensions = rooms.ToDictionary(
            room => room.Node.Index,
            room => DungeonMapGeometry.BoundsFor(room.Role, new Point(0, 0), random)
        );
        var span = RequiredSpan(layout, dimensions);
        var bounds = rooms.ToDictionary(
            room => room.Node.Index,
            room =>
                DungeonMapGeometry.BoundsAt(
                    OffsetRoomCenter(
                        RoomCenter(layout, room.Node, span, dimensions),
                        room.Node,
                        random
                    ),
                    dimensions[room.Node.Index].Right - dimensions[room.Node.Index].Left,
                    dimensions[room.Node.Index].Bottom - dimensions[room.Node.Index].Top
                )
        );
        return new DungeonFloorPlan(layout, bounds);
    }

    public Polyline? PathFor(DungeonPassage passage)
    {
        var from = layout.Rooms[passage.From];
        var to = layout.Rooms[passage.To];
        if (from.FloorNumber != to.FloorNumber)
            return null;
        if (from.IsDeadEnd || to.IsDeadEnd)
            return DungeonMapGeometry.PathBetween(bounds[from.Index], bounds[to.Index]);
        if (IsReturnRoom(from) || IsReturnRoom(to))
            return ReturnPath(from, to);
        if (from.Index == layout.EntranceIndex)
            return EntrancePath(to);
        if (to.Index == layout.BossIndex || to.Index == layout.LandingIndex)
            return ConvergencePath(from, to);
        if (from.Index == layout.BossIndex || from.Index == layout.LandingIndex)
            return DungeonMapGeometry.Reverse(ConvergencePath(to, from));
        if (from.RouteKind == DungeonRouteKind.Long)
            return DungeonMapGeometry.PathBetween(bounds[from.Index], bounds[to.Index]);
        return HorizontalPath(from.Index, to.Index, LaneFor(from.RouteKind));
    }

    private static Point RoomCenter(
        DungeonLayout layout,
        DungeonRoomNode room,
        int span,
        IReadOnlyDictionary<int, Rectangle> dimensions
    )
    {
        if (room.Index == layout.EntranceIndex)
            return new Point(0, 0);
        if (room.Index == layout.LandingIndex)
            return new Point(span, 0);
        if (room.Index == layout.BossIndex)
            return new Point(
                span + (layout.LandingIndex.HasValue && room.FloorNumber == 0 ? 56 : 0),
                0
            );
        if (room.RouteKind == DungeonRouteKind.None)
            return ReturnRoomCenter(layout, room, span);
        if (room.IsDeadEnd)
            return DeadEndCenter(layout, room, span, dimensions);
        if (room.RouteKind == DungeonRouteKind.Long)
            return LongRoomCenter(layout, room, span, dimensions);
        return new Point(
            CoordinateOnChain(CompleteRoute(layout, room.RouteKind), dimensions, span, room.Index),
            room.RouteKind == DungeonRouteKind.Short && HasThirdRoute(layout) ? 48 : 0
        );
    }

    private static Point DeadEndCenter(
        DungeonLayout layout,
        DungeonRoomNode room,
        int span,
        IReadOnlyDictionary<int, Rectangle> dimensions
    )
    {
        var passage = layout.Passages.Single(passage =>
            passage.From == room.Index || passage.To == room.Index
        );
        var parent = layout.Rooms[passage.From == room.Index ? passage.To : passage.From];
        var parentCenter = LongRoomCenter(layout, parent, span, dimensions);
        return parentCenter.X == 0 ? new Point(-BranchSpacing, parentCenter.Y)
            : parentCenter.X == span ? new Point(span + BranchSpacing, parentCenter.Y)
            : new Point(parentCenter.X, parentCenter.Y - BranchSpacing);
    }

    private int LaneFor(DungeonRouteKind kind) =>
        kind switch
        {
            DungeonRouteKind.Long => LongLane,
            DungeonRouteKind.Short => ShortLane,
            _ => 0,
        };

    private static bool HasThirdRoute(DungeonLayout layout) =>
        layout.Rooms.Any(room => room.RouteKind == DungeonRouteKind.Third);

    private static Point LongRoomCenter(
        DungeonLayout layout,
        DungeonRoomNode room,
        int span,
        IReadOnlyDictionary<int, Rectangle> dimensions
    )
    {
        var chain = RouteRooms(layout, DungeonRouteKind.Long);
        var count = chain.Length;
        if (count <= 4)
            return new Point(CoordinateOnChain(chain, dimensions, span, room.Index), LongLane);
        if (room.DepthFromEntrance == 1)
            return new Point(0, LongLane);
        if (room.DepthFromEntrance == count)
            return new Point(span, LongLane);
        return new Point(
            CoordinateOnChain(
                chain.Skip(1).Take(count - 2).ToArray(),
                dimensions,
                span,
                room.Index
            ),
            2 * LongLane
        );
    }

    private static Point ReturnRoomCenter(DungeonLayout layout, DungeonRoomNode room, int span)
    {
        var hasFiller = layout.Rooms.Any(candidate =>
            candidate.RouteKind == DungeonRouteKind.None
            && candidate.Index != layout.EntranceIndex
            && candidate.Index != layout.BossIndex
            && candidate.Index != layout.LandingIndex
            && candidate.Index != layout.BackDoorIndex
        );
        var fraction = hasFiller
            ? room.Index == layout.BackDoorIndex
                ? 0.25
                : 0.75
            : 0.5;
        return new Point(Math.Round(span * fraction), HasThirdRoute(layout) ? 88 : 40);
    }

    private static int RequiredSpan(
        DungeonLayout layout,
        IReadOnlyDictionary<int, Rectangle> dimensions
    )
    {
        var longRooms = RouteRooms(layout, DungeonRouteKind.Long);
        var topRow =
            longRooms.Length <= 4
                ? longRooms
                : longRooms.Skip(1).Take(longRooms.Length - 2).ToArray();
        return (int)
            Math.Ceiling(
                new[]
                {
                    80d,
                    ChainSpan(topRow, dimensions),
                    ChainSpan(CompleteRoute(layout, DungeonRouteKind.Short), dimensions),
                    ChainSpan(CompleteRoute(layout, DungeonRouteKind.Third), dimensions),
                    RequiredReturnSpan(layout, dimensions),
                }.Max()
            );
    }

    private static double RequiredReturnSpan(
        DungeonLayout layout,
        IReadOnlyDictionary<int, Rectangle> dimensions
    )
    {
        var returnRooms = layout
            .Rooms.Where(room =>
                room.RouteKind == DungeonRouteKind.None
                && room.Index != layout.EntranceIndex
                && room.Index != layout.BossIndex
                && room.Index != layout.LandingIndex
            )
            .Select(room => room.Index)
            .ToArray();
        return returnRooms.Length == 2 ? 2 * ChainSpan(returnRooms, dimensions) : 0;
    }

    private static int[] CompleteRoute(DungeonLayout layout, DungeonRouteKind kind) =>
        new[] { layout.EntranceIndex }
            .Concat(RouteRooms(layout, kind))
            .Append(layout.LandingIndex ?? layout.BossIndex)
            .ToArray();

    private static int[] RouteRooms(DungeonLayout layout, DungeonRouteKind kind) =>
        layout
            .Rooms.Where(room => room.RouteKind == kind && !room.IsDeadEnd)
            .OrderBy(room => room.DepthFromEntrance)
            .Select(room => room.Index)
            .ToArray();

    private static double ChainSpan(
        IReadOnlyList<int> chain,
        IReadOnlyDictionary<int, Rectangle> dimensions
    ) =>
        chain
            .Zip(
                chain.Skip(1),
                (from, to) =>
                    (
                        dimensions[from].Right
                        - dimensions[from].Left
                        + dimensions[to].Right
                        - dimensions[to].Left
                    ) / 2d
                    + RoomGap
            )
            .Sum();

    private static Point OffsetRoomCenter(Point center, DungeonRoomNode room, Random random) =>
        room.RouteKind == DungeonRouteKind.None ? center
        : room.IsDeadEnd ? new Point(center.X + random.Next(-2, 3), center.Y)
        : new Point(center.X, center.Y + random.Next(-2, 3));

    private static double CoordinateOnChain(
        IReadOnlyList<int> chain,
        IReadOnlyDictionary<int, Rectangle> dimensions,
        int span,
        int roomIndex
    )
    {
        var additionalGap = (span - ChainSpan(chain, dimensions)) / (chain.Count - 1);
        var position = 0d;
        for (var index = 0; index < chain.Count - 1; index++)
        {
            if (chain[index] == roomIndex)
                return Math.Round(position);
            var current = dimensions[chain[index]];
            var next = dimensions[chain[index + 1]];
            position +=
                (current.Right - current.Left + next.Right - next.Left) / 2d
                + RoomGap
                + additionalGap;
        }
        if (chain[^1] == roomIndex)
            return span;
        throw new InvalidOperationException("Room does not belong to the corridor chain.");
    }

    private bool IsReturnRoom(DungeonRoomNode room) =>
        room.RouteKind == DungeonRouteKind.None
        && room.Index != layout.EntranceIndex
        && room.Index != layout.BossIndex
        && room.Index != layout.LandingIndex;

    private Polyline HorizontalPath(int from, int to, double lane)
    {
        var movingRight = bounds[to].Left > bounds[from].Left;
        return new Polyline
        {
            Points =
            [
                new(movingRight ? bounds[from].Right : bounds[from].Left, lane),
                new(movingRight ? bounds[to].Left : bounds[to].Right, lane),
            ],
        };
    }

    private Polyline EntrancePath(DungeonRoomNode destination)
    {
        var entrance = bounds[layout.EntranceIndex];
        var target = bounds[destination.Index];
        if (destination.RouteKind == DungeonRouteKind.Long)
            return DungeonMapGeometry.PathBetween(entrance, target);
        var lane = LaneFor(destination.RouteKind);
        if (lane == 0)
            return HorizontalPath(layout.EntranceIndex, destination.Index, lane);
        var doorX = DungeonMapGeometry.Center(entrance).X + (lane > 0 ? 6 : 0);
        return new Polyline
        {
            Points =
            [
                new(doorX, lane < 0 ? entrance.Top : entrance.Bottom),
                new(doorX, lane),
                new(target.Left, lane),
            ],
        };
    }

    private Polyline ConvergencePath(DungeonRoomNode origin, DungeonRoomNode destination)
    {
        var source = bounds[origin.Index];
        var target = bounds[destination.Index];
        if (origin.RouteKind == DungeonRouteKind.Long)
            return DungeonMapGeometry.PathBetween(source, target);
        var lane = LaneFor(origin.RouteKind);
        if (lane == 0)
            return HorizontalPath(origin.Index, destination.Index, lane);
        var doorX = DungeonMapGeometry.Center(target).X - (lane > 0 ? 6 : 0);
        return new Polyline
        {
            Points =
            [
                new(source.Right, lane),
                new(doorX, lane),
                new(doorX, lane < 0 ? target.Top : target.Bottom),
            ],
        };
    }

    private Polyline ReturnPath(DungeonRoomNode from, DungeonRoomNode to)
    {
        if (IsReturnRoom(from) && IsReturnRoom(to))
            return HorizontalPath(from.Index, to.Index, ReturnLane);
        if (IsReturnRoom(from))
            return DungeonMapGeometry.Reverse(ReturnPath(to, from));
        var source = bounds[from.Index];
        var target = bounds[to.Index];
        var isEntrance = from.Index == layout.EntranceIndex;
        var outerX = isEntrance ? source.Left - 12 : source.Right + 12;
        return new Polyline
        {
            Points =
            [
                new(isEntrance ? source.Left : source.Right, 0),
                new(outerX, 0),
                new(outerX, ReturnLane),
                new(isEntrance ? target.Left : target.Right, ReturnLane),
            ],
        };
    }
}
