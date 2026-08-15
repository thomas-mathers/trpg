using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Buildings.Commands;

public class SetFrontDoorLockedCommand
{
    public required Guid BuildingId { get; init; }
    public required bool IsLocked { get; init; }
}

public class SetFrontDoorLockedCommandHandler(TrpgDbContext context)
{
    public async Task<bool?> Handle(
        SetFrontDoorLockedCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var updatedCount = await (
            from door in context.DoorConnectors
            join c in context.LocationConnectors on door.ConnectorId equals c.Id
            join destination in context.Locations on c.DestinationLocationId equals destination.Id
            join r in context.Rooms on c.OriginLocationId equals r.LocationId
            where
                r.BuildingId == command.BuildingId
                && r.FloorNumber == 0
                && destination.RoomId == null
            select door
        ).ExecuteUpdateAsync(
            s => s.SetProperty(c => c.IsLocked, command.IsLocked),
            cancellationToken
        );

        return updatedCount > 0 ? command.IsLocked : null;
    }
}
