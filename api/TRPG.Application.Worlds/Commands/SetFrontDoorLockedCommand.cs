using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Commands;

public class SetFrontDoorLockedCommand
{
    public required Guid BuildingId { get; init; }
    public required bool IsLocked { get; init; }
}

internal class SetFrontDoorLockedCommandHandler(IWorldsDbContext context)
    : ICommandHandler<SetFrontDoorLockedCommand, bool?>
{
    public async Task<bool?> Handle(
        SetFrontDoorLockedCommand command,
        CancellationToken cancellationToken = default
    )
    {
        // Only the entry connector (exterior -> entrance room) carries a lockable door.
        var updatedCount = await (
            from door in context.DoorConnectors
            join c in context.LocationConnectors on door.ConnectorId equals c.Id
            join origin in context.Locations on c.OriginLocationId equals origin.Id
            join r in context.Rooms on c.DestinationLocationId equals r.LocationId
            where r.BuildingId == command.BuildingId && r.FloorNumber == 0 && origin.RoomId == null
            select door
        ).ExecuteUpdateAsync(
            s => s.SetProperty(c => c.IsLocked, command.IsLocked),
            cancellationToken
        );

        return updatedCount > 0 ? command.IsLocked : null;
    }
}
