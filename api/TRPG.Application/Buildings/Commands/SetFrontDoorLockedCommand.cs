using Microsoft.EntityFrameworkCore;
using TRPG.Application.Buildings.Queries;
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
            from door in context.DoorConnectors
            join c in context.LocationConnectors.WhereLeadsOutside(context)
                on door.ConnectorId equals c.Id
            join r in context.Rooms on c.OriginLocationId equals r.LocationId
            where r.BuildingId == command.BuildingId && r.FloorNumber == 0
            select door
        ).ExecuteUpdateAsync(
            s => s.SetProperty(c => c.IsLocked, command.IsLocked),
            cancellationToken
        );

        return updatedCount > 0 ? command.IsLocked : null;
    }
}
