using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Commands;

public class SetFrontDoorsLockedCommand
{
    public required IReadOnlyCollection<Guid> BuildingIds { get; init; }
    public required bool IsLocked { get; init; }
}

internal class SetFrontDoorsLockedCommandHandler(
    IWorldsDbContext context,
    IDomainEventPublisher<DoorLocksChangedEvent> doorLocksChanged
) : ICommandHandler<SetFrontDoorsLockedCommand>
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
        var changedDoors = await (
            from door in context.DoorConnectors
            join c in context.LocationConnectors on door.ConnectorId equals c.Id
            join origin in context.Locations on c.OriginLocationId equals origin.Id
            join r in context.Rooms on c.DestinationLocationId equals r.LocationId
            where
                command.BuildingIds.AsEnumerable().Contains(r.BuildingId)
                && r.FloorNumber == 0
                && origin.RoomId == null
                && door.IsLocked != command.IsLocked
            select new
            {
                door.Id,
                LocationId = origin.Id,
                origin.WorldId,
            }
        ).ToArrayAsync(cancellationToken);
        if (changedDoors.Length == 0)
        {
            return;
        }

        var doorIds = changedDoors.Select(door => door.Id).ToArray();
        await context
            .DoorConnectors.Where(door => doorIds.Contains(door.Id))
            .ExecuteUpdateAsync(
                s => s.SetProperty(door => door.IsLocked, command.IsLocked),
                cancellationToken
            );

        foreach (var world in changedDoors.GroupBy(door => door.WorldId))
        {
            await doorLocksChanged.Publish(
                new DoorLocksChangedEvent(
                    world.Key,
                    world.Select(door => door.LocationId).Distinct().ToArray()
                ),
                cancellationToken
            );
        }
    }
}
