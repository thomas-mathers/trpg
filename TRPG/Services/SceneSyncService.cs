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
            await CatchUpRoom(roomId.Value, currentDate, cancellationToken);
            await SyncFrontDoorLock(roomId.Value, currentDate, cancellationToken);
        }
        else if (districtId != null) {
            await CatchUpDistrict(worldId, districtId.Value, currentDate, cancellationToken);
        }

        session.LastCatchUpScopeId = scopeId;
        session.LastCatchUpDate = currentDate;
        return true;
    }

    private async Task CatchUpRoom(Guid roomId, InGameDate currentDate, CancellationToken cancellationToken) {
        var creatureIds = await jobService.GetCreatureIdsByRoomId(roomId, cancellationToken);
        await CatchUp("Room", creatureIds, currentDate, cancellationToken);
    }

    private async Task CatchUpDistrict(Guid worldId, Guid districtId, InGameDate currentDate,
        CancellationToken cancellationToken) {
        var creatureIds = await creatureService.GetIdsByDistrict(worldId, districtId, cancellationToken);
        await CatchUp("District", creatureIds, currentDate, cancellationToken);
    }

    private async Task CatchUp(string scope, IReadOnlyCollection<Guid> creatureIds, InGameDate currentDate,
        CancellationToken cancellationToken) {
        var stopwatch = Stopwatch.StartNew();
        var workingCreaturesByRoomId = new Dictionary<Guid, List<Guid>>();

        foreach (var creatureId in creatureIds) {
            var jobs = await jobService.GetAllByCreatureId(creatureId, cancellationToken);

            var dueJob = jobs
                .Where(j => JobScheduling.IsActiveAtHour(j, currentDate.Weekday, currentDate.Hour))
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

            if (dueJob.Action == JobAction.Work && dueJob.RoomId != null) {
                workingCreaturesByRoomId.TryAdd(dueJob.RoomId.Value, []);
                workingCreaturesByRoomId[dueJob.RoomId.Value].Add(creatureId);
            }
        }

        foreach (var (roomId, presentCreatureIds) in workingCreaturesByRoomId) {
            await AssignWorkstations(roomId, presentCreatureIds, cancellationToken);
        }

        stopwatch.Stop();

        logger.LogInformation("[perf] CatchUp{Scope} processed {CreatureCount} people in {ElapsedMs}ms",
            scope, creatureIds.Count, stopwatch.ElapsedMilliseconds);
    }

    // Whoever's working this shift always has one person at the counter first; anyone else present fills
    // whatever production workstation is free. Any station nobody's left to staff gets cleared.
    private async Task AssignWorkstations(Guid roomId, IReadOnlyList<Guid> presentCreatureIds,
        CancellationToken cancellationToken) {
        var workstations = await buildingService.GetWorkstationsByRoomId(roomId, cancellationToken);
        var counter = workstations.Where(w => w.WorkstationType == WorkstationType.Trade);
        var productionStations = workstations.Where(w => w.WorkstationType != WorkstationType.Trade);
        var orderedStations = counter.Concat(productionStations).ToArray();

        var remainingCreatureIds = new Queue<Guid>(presentCreatureIds);
        foreach (var station in orderedStations) {
            var occupantId = remainingCreatureIds.Count > 0 ? remainingCreatureIds.Dequeue() : (Guid?) null;
            await buildingService.SetWorkstationOccupant(station.Id, occupantId, cancellationToken);
        }
    }

    private async Task SyncFrontDoorLock(Guid roomId, InGameDate currentDate, CancellationToken cancellationToken) {
        var roomSummary = await buildingService.GetRoomSummary(roomId, cancellationToken);
        if (roomSummary == null || roomSummary.RoomFloorNumber != 0) {
            return;
        }

        await SyncScheduleLock(roomSummary.BuildingId, roomSummary.BuildingType, currentDate, cancellationToken);
    }

    public async Task<bool?> SyncScheduleLock(Guid buildingId, BuildingType buildingType, InGameDate currentDate,
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
            .Where(j => JobScheduling.IsActiveAtHour(j, currentDate.Weekday, currentDate.Hour))
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
