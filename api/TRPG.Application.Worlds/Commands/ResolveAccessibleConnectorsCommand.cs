using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Commands;

public class ResolveAccessibleConnectorsCommand
{
    public required IReadOnlySet<Guid> PlayerKeyItemIds { get; init; }
    public required IReadOnlySet<Guid> ActivatedTriggerIds { get; init; }
    public required GameInstant GameTime { get; init; }
    public required IReadOnlyCollection<Guid> ConnectorIds { get; init; }
}

internal class ResolveAccessibleConnectorsCommandHandler(
    IQueryHandler<
        GetDoorConnectorsByConnectorIdsQuery,
        IReadOnlyDictionary<Guid, DoorConnector>
    > getDoorConnectorsByConnectorIds,
    ICommandHandler<SetDoorTimedLockCommand> setDoorTimedLock,
    IQueryHandler<
        GetKeyItemIdsByDoorConnectorIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getKeyItemIdsByDoorConnectorIds,
    IQueryHandler<
        GetLeverIdsByDoorConnectorIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getLeverIdsByDoorConnectorIds
) : ICommandHandler<ResolveAccessibleConnectorsCommand, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        ResolveAccessibleConnectorsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var doorsByConnectorId = await getDoorConnectorsByConnectorIds.Handle(
            new GetDoorConnectorsByConnectorIdsQuery { ConnectorIds = command.ConnectorIds },
            cancellationToken
        );

        var lockedDoors = doorsByConnectorId.Values.Where(door => door.IsLocked).ToArray();
        if (lockedDoors.Length == 0)
        {
            return command.ConnectorIds;
        }

        var elapsedDoorIds = await ClearElapsedTimedLocks(
            command.GameTime,
            lockedDoors,
            cancellationToken
        );
        var stillLockedDoors = lockedDoors
            .Where(door => !elapsedDoorIds.Contains(door.Id))
            .ToArray();
        if (stillLockedDoors.Length == 0)
        {
            return command.ConnectorIds;
        }

        var doorIds = stillLockedDoors.Select(door => door.Id).ToArray();
        var keyItemIdsByDoor = await getKeyItemIdsByDoorConnectorIds.Handle(
            new GetKeyItemIdsByDoorConnectorIdsQuery { DoorConnectorIds = doorIds },
            cancellationToken
        );
        var leverIdsByDoor = await getLeverIdsByDoorConnectorIds.Handle(
            new GetLeverIdsByDoorConnectorIdsQuery { DoorConnectorIds = doorIds },
            cancellationToken
        );

        var inaccessibleConnectorIds = new HashSet<Guid>();

        foreach (var door in stillLockedDoors)
        {
            var validKeyItemIds = keyItemIdsByDoor.GetValueOrDefault(door.Id, []);
            if (command.PlayerKeyItemIds.Overlaps(validKeyItemIds))
            {
                continue;
            }

            var requiredLeverIds = leverIdsByDoor.GetValueOrDefault(door.Id, []);
            if (
                requiredLeverIds.Count > 0
                && requiredLeverIds.All(command.ActivatedTriggerIds.Contains)
            )
            {
                continue;
            }

            // A lock with no key or lever ever configured would otherwise soft-lock the building
            // forever, so it's not enforced.
            if (
                door.UnlocksAtGameTime != null
                || validKeyItemIds.Count > 0
                || requiredLeverIds.Count > 0
            )
            {
                inaccessibleConnectorIds.Add(door.ConnectorId);
            }
        }

        return command.ConnectorIds.Where(id => !inaccessibleConnectorIds.Contains(id)).ToArray();
    }

    private async Task<HashSet<Guid>> ClearElapsedTimedLocks(
        GameInstant gameTime,
        IReadOnlyCollection<DoorConnector> lockedDoors,
        CancellationToken cancellationToken
    )
    {
        var doorsWithSchedule = lockedDoors.Where(door => door.UnlocksAtGameTime != null).ToArray();
        if (doorsWithSchedule.Length == 0)
        {
            return [];
        }

        var elapsedDoorIds = doorsWithSchedule
            .Where(door => gameTime >= door.UnlocksAtGameTime!.Value)
            .Select(door => door.Id)
            .ToArray();

        if (elapsedDoorIds.Length > 0)
        {
            await setDoorTimedLock.Handle(
                new SetDoorTimedLockCommand
                {
                    DoorConnectorIds = elapsedDoorIds,
                    UnlocksAtGameTime = null,
                },
                cancellationToken
            );
        }

        return elapsedDoorIds.ToHashSet();
    }
}
