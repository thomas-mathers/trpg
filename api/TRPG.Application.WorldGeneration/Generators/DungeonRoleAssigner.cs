using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record AssignedDungeonRoom(DungeonRoomNode Node, RoomRole Role);

internal static class DungeonRoleAssigner
{
    public static IReadOnlyList<AssignedDungeonRoom> Assign(
        DungeonLayout layout,
        BuildingType dungeonType,
        Random random
    )
    {
        var available = DungeonRoomCatalog.RolesFor(dungeonType).ToArray();
        var paying = available.Where(DungeonRoomCatalog.PayingRoles.Contains).ToArray();

        return layout
            .Rooms.Select(room => new AssignedDungeonRoom(
                room,
                RoleFor(room, layout, paying, available, random)
            ))
            .ToArray();
    }

    private static RoomRole RoleFor(
        DungeonRoomNode room,
        DungeonLayout layout,
        IReadOnlyList<RoomRole> paying,
        IReadOnlyList<RoomRole> available,
        Random random
    )
    {
        if (room.Index == layout.EntranceIndex)
        {
            return RoomRole.Entrance;
        }

        if (room.Index == layout.BossIndex)
        {
            return RoomRole.BossChamber;
        }

        // Walking to a dead end has to be worth it, so these draw only from roles that pay. A type
        // with none of them falls back to a treasure room rather than wasting the walk.
        if (room.IsDeadEnd)
        {
            return paying.Count == 0 ? RoomRole.TreasureRoom : paying[random.Next(paying.Count)];
        }

        return available[random.Next(available.Count)];
    }
}
