using TRPG.Application.Inventory.Queries;

namespace TRPG.Application.Buildings.Queries;

internal class CanEnterBuildingQuery
{
    public required Guid EntranceRoomId { get; init; }
    public required Guid EnteringCreatureId { get; init; }
}

internal class CanEnterBuildingQueryHandler(
    GetFrontDoorQueryHandler getFrontDoor,
    GetKeyItemIdsQueryHandler getKeyItemIds,
    GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId
)
{
    public async Task<bool> Handle(
        CanEnterBuildingQuery buildingQuery,
        CancellationToken cancellationToken = default
    )
    {
        var door = await getFrontDoor.Handle(
            new GetFrontDoorQuery { RoomId = buildingQuery.EntranceRoomId },
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
            new GetInventoryByCreatureIdQuery { CreatureId = buildingQuery.EnteringCreatureId },
            cancellationToken
        );
        return inventory.Any(i => validKeyItemIds.Contains(i.ItemId));
    }
}
