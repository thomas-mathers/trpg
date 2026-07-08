using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Models;

namespace TRPG.Services;

internal class SceneSyncService(
    JobService jobService,
    CreatureService creatureService,
    JobDispatcher dispatcher,
    BuildingService buildingService,
    ILogger<SceneSyncService> logger
) {
    private static readonly HashSet<BuildingType> NeverLocked = [BuildingType.Inn, BuildingType.Tavern];
    
    public async Task<bool> SyncIfNeeded(GameSession session, Guid worldId, Guid? roomId, Guid? districtId,
        InGameDate currentDate, CancellationToken cancellationToken = default) {
        var scopeId = roomId ?? districtId;
        if (scopeId == session.LastCatchUpScopeId && currentDate == session.LastCatchUpDate) {
            return false;
        }

        if (roomId != null) {
            await CatchUpRoom(roomId.Value, currentDate.Hour, cancellationToken);
            await SyncFrontDoorLock(roomId.Value, currentDate.Hour, cancellationToken);
        }
        else if (districtId != null) {
            await CatchUpDistrict(worldId, districtId.Value, currentDate.Hour, cancellationToken);
        }

        session.LastCatchUpScopeId = scopeId;
        session.LastCatchUpDate = currentDate;
        return true;
    }

    private async Task CatchUpRoom(Guid roomId, int hour, CancellationToken cancellationToken) {
        var creatureIds = await jobService.GetCreatureIdsByRoomId(roomId, cancellationToken);
        await CatchUp("Room", creatureIds, hour, cancellationToken);
    }

    private async Task CatchUpDistrict(Guid worldId, Guid districtId, int hour, CancellationToken cancellationToken) {
        var creatureIds = await creatureService.GetIdsByDistrict(worldId, districtId, cancellationToken);
        await CatchUp("District", creatureIds, hour, cancellationToken);
    }

    private async Task CatchUp(string scope, IReadOnlyCollection<Guid> creatureIds, int hour,
        CancellationToken cancellationToken) {
        var stopwatch = Stopwatch.StartNew();

        foreach (var creatureId in creatureIds) {
            var jobs = await jobService.GetAllByCreatureId(creatureId, cancellationToken);

            var dueJob = jobs
                .Where(j => JobScheduling.IsActiveAtHour(j, hour))
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.Id)
                .FirstOrDefault();

            if (dueJob == null) {
                continue;
            }

            var creature = await creatureService.GetById(creatureId, cancellationToken);

            if (creature != null) {
                await dispatcher.Dispatch(creature, dueJob, cancellationToken);
            }
        }

        stopwatch.Stop();
        
        logger.LogInformation("[perf] CatchUp{Scope} processed {CreatureCount} people in {ElapsedMs}ms",
            scope, creatureIds.Count, stopwatch.ElapsedMilliseconds);
    }
    
    private async Task SyncFrontDoorLock(Guid roomId, int hour, CancellationToken cancellationToken) {
        var roomSummary = await buildingService.GetRoomSummary(roomId, cancellationToken);
        if (roomSummary == null || roomSummary.RoomFloorNumber != 0) {
            return;
        }

        await SyncScheduleLock(roomSummary.BuildingId, roomSummary.BuildingType, hour, cancellationToken);
    }

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
}
