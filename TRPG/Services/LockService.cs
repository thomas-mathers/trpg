using TRPG.Models;

namespace TRPG.Services;

internal class LockService(BuildingService buildingService, JobService jobService, InventoryService inventoryService) {
    private static readonly HashSet<BuildingType> NeverLocked = [BuildingType.Inn, BuildingType.Tavern];

    public async Task<bool?> SyncScheduleLock(Guid buildingId, BuildingType buildingType, int hour,
        CancellationToken cancellationToken = default) {
        if (NeverLocked.Contains(buildingType)) {
            return null;
        }

        var owners = await buildingService.GetAllOwnersByBuildingId(buildingId, cancellationToken);
        var ownerId = owners.FirstOrDefault()?.OwnerId;
        if (ownerId == null) {
            return null;
        }

        var jobs = await jobService.GetAllByCreatureId(ownerId.Value, cancellationToken);
        var activeJob = jobs
            .Where(j => JobScheduling.IsActiveAtHour(j, hour))
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.Id)
            .FirstOrDefault();
        if (activeJob == null) {
            return null;
        }

        return await buildingService.SetFrontDoorLocked(buildingId, activeJob.Action == JobAction.Sleep,
            cancellationToken);
    }

    public async Task<bool> CanEnter(Guid entranceRoomId, Guid enteringCreatureId,
        CancellationToken cancellationToken = default) {
        var door = await buildingService.GetFrontDoor(entranceRoomId, cancellationToken);
        if (door is not { IsLocked: true }) {
            return true;
        }

        var validKeyItemIds = await buildingService.GetKeyItemIds(door.Id, cancellationToken);
        if (validKeyItemIds.Count == 0) {
            return true;
        }

        var inventory = await inventoryService.GetAllByCreatureId(enteringCreatureId, cancellationToken);
        return inventory.Any(i => validKeyItemIds.Contains(i.ItemId));
    }
}