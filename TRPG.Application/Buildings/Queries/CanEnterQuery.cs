using TRPG.Application.Buildings.Queries;
using TRPG.Application.Inventory.Queries;

namespace TRPG.Application.Buildings.Queries;

internal class CanEnterQuery
{
    public required Guid EntranceRoomId { get; init; }
    public required Guid EnteringCreatureId { get; init; }
}

internal class CanEnterQueryHandler(
    GetFrontDoorQueryHandler getFrontDoor,
    GetKeyItemIdsQueryHandler getKeyItemIds,
    GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId
)
{
    public async Task<bool> Handle(
        CanEnterQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var door = await getFrontDoor.Handle(
            new GetFrontDoorQuery { RoomId = query.EntranceRoomId },
            cancellationToken
        );
        if (door is not { IsLocked: true })
        {
            return true;
        }

        var validKeyItemIds = await getKeyItemIds.Handle(
            new GetKeyItemIdsQuery { RoomConnectorId = door.Id },
            cancellationToken
        );
        if (validKeyItemIds.Count == 0)
        {
            return true;
        }

        var inventory = await getInventoryByCreatureId.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = query.EnteringCreatureId },
            cancellationToken
        );
        return inventory.Any(i => validKeyItemIds.Contains(i.ItemId));
    }
}
