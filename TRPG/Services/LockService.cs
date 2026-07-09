namespace TRPG.Services;

internal class LockService(BuildingService buildingService, InventoryService inventoryService)
{
    public async Task<bool> CanEnter(
        Guid entranceRoomId,
        Guid enteringCreatureId,
        CancellationToken cancellationToken = default
    )
    {
        var door = await buildingService.GetFrontDoor(entranceRoomId, cancellationToken);
        if (door is not { IsLocked: true })
        {
            return true;
        }

        var validKeyItemIds = await buildingService.GetKeyItemIds(door.Id, cancellationToken);
        if (validKeyItemIds.Count == 0)
        {
            return true;
        }

        var inventory = await inventoryService.GetAllByCreatureId(
            enteringCreatureId,
            cancellationToken
        );
        return inventory.Any(i => validKeyItemIds.Contains(i.ItemId));
    }
}
