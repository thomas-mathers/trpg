using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Commands;

public class SetFrontDoorsLockedCommand
{
    public required IReadOnlyCollection<Guid> BuildingIds { get; init; }
    public required bool IsLocked { get; init; }
}

internal class SetFrontDoorsLockedCommandHandler(IWorldsDbContext context)
    : ICommandHandler<SetFrontDoorsLockedCommand>
{
    public async Task Handle(
        SetFrontDoorsLockedCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.BuildingIds.Count == 0)
        {
            return;
        }

        // Only the entry connector (exterior -> entrance room) carries a lockable door.
        await (
            from door in context.DoorConnectors
            join c in context.LocationConnectors on door.ConnectorId equals c.Id
            join origin in context.Locations on c.OriginLocationId equals origin.Id
            join r in context.Rooms on c.DestinationLocationId equals r.LocationId
            where
                command.BuildingIds.AsEnumerable().Contains(r.BuildingId)
                && r.FloorNumber == 0
                && origin.RoomId == null
            select door
        ).ExecuteUpdateAsync(
            s => s.SetProperty(c => c.IsLocked, command.IsLocked),
            cancellationToken
        );
    }
}
