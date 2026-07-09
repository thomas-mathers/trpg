using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Commands;

internal class SetFrontDoorLockedCommand
{
    public required Guid BuildingId { get; init; }
    public required bool IsLocked { get; init; }
}

internal class SetFrontDoorLockedCommandHandler(TrpgDbContext context)
{
    public async Task<bool?> Handle(
        SetFrontDoorLockedCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var updatedCount = await (
            from c in context.Props.OfType<RoomConnector>()
            join r in context.Rooms on c.RoomId equals r.Id
            where
                r.BuildingId == command.BuildingId
                && r.FloorNumber == 0
                && c.DestinationRoomId == null
            select c
        ).ExecuteUpdateAsync(
            s => s.SetProperty(c => c.IsLocked, command.IsLocked),
            cancellationToken
        );

        return updatedCount > 0 ? command.IsLocked : null;
    }
}
