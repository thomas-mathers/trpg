using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Queries;

public class GetLocalMapLocksQuery
{
    public required IReadOnlyCollection<Guid> ConnectorIds { get; init; }
}

public enum LocalMapLockKind
{
    None,
    LockedDoor,
    KeyLockedDoor,
    Portcullis,
}

internal class GetLocalMapLocksQueryHandler(
    IQueryHandler<
        GetDoorConnectorsByConnectorIdsQuery,
        IReadOnlyDictionary<Guid, DoorConnector>
    > getDoors,
    IQueryHandler<
        GetLeverIdsByDoorConnectorIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getLevers,
    IQueryHandler<
        GetKeyItemIdsByDoorConnectorIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getKeys
) : IQueryHandler<GetLocalMapLocksQuery, IReadOnlyDictionary<Guid, LocalMapLockKind>>
{
    public async Task<IReadOnlyDictionary<Guid, LocalMapLockKind>> Handle(
        GetLocalMapLocksQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var doors = await getDoors.Handle(
            new GetDoorConnectorsByConnectorIdsQuery { ConnectorIds = query.ConnectorIds },
            cancellationToken
        );
        var lockedDoorIds = doors
            .Values.Where(door => door.IsLocked)
            .Select(door => door.Id)
            .ToArray();

        var levers = await getLevers.Handle(
            new GetLeverIdsByDoorConnectorIdsQuery { DoorConnectorIds = lockedDoorIds },
            cancellationToken
        );

        var keys = await getKeys.Handle(
            new GetKeyItemIdsByDoorConnectorIdsQuery { DoorConnectorIds = lockedDoorIds },
            cancellationToken
        );
        return doors.ToDictionary(
            pair => pair.Key,
            pair =>
                !pair.Value.IsLocked ? LocalMapLockKind.None
                : levers.ContainsKey(pair.Value.Id) ? LocalMapLockKind.Portcullis
                : keys.ContainsKey(pair.Value.Id) ? LocalMapLockKind.KeyLockedDoor
                : LocalMapLockKind.LockedDoor
        );
    }
}
