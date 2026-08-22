using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data;

namespace TRPG.Application.Buildings.Commands;

public class SetFrontDoorLockedCommand
{
    public required Guid BuildingId { get; init; }
    public required bool IsLocked { get; init; }
}

internal class SetFrontDoorLockedCommandHandler(TrpgDbContext context)
    : ICommandHandler<SetFrontDoorLockedCommand, bool?>
{
    public async Task<bool?> Handle(
        SetFrontDoorLockedCommand command,
        CancellationToken cancellationToken = default
    )
    {
        // The lockable door lives on the entry connector (exterior -> entrance room), not the
        // exit connector (entrance room -> exterior) — someone already inside can always leave.
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
