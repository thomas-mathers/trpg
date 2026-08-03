using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.CreatureJobs;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Scenes.Commands;

internal class SyncCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required InGameDate CurrentDate { get; init; }
}

internal class SyncCommandHandler(
    GetLocationByIdQueryHandler getLocationById,
    GetCreatureIdsWithCreatureJobInLocationQueryHandler getCreatureIdsWithJobInLocation,
    GetAllCreatureJobsByCreatureIdQueryHandler getAllJobsByCreatureId,
    GetCreatureIdsByDistrictQueryHandler getCreatureIdsByDistrict,
    GetCreatureByIdQueryHandler getCreatureById,
    ExecuteCreatureJobCommandHandler executeJob,
    GetWorkstationsByRoomIdQueryHandler getWorkstationsByRoomId,
    SetWorkstationOccupantCommandHandler setWorkstationOccupant,
    GetRoomSummaryQueryHandler getRoomSummary,
    SyncScheduleLockCommandHandler syncScheduleLock,
    ILogger<SyncCommandHandler> logger
)
{
    public async Task Handle(SyncCommand command, CancellationToken cancellationToken = default)
    {
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.LocationId },
            cancellationToken
        );
        if (location == null)
        {
            return;
        }

        if (location.RoomId != null)
        {
            await CatchUpRoom(command.LocationId, command.CurrentDate, cancellationToken);
            await SyncFrontDoorLock(location.RoomId.Value, command.CurrentDate, cancellationToken);
        }
        else if (location.DistrictId != null)
        {
            await CatchUpDistrict(
                command.WorldId,
                location.DistrictId.Value,
                command.CurrentDate,
                cancellationToken
            );
        }
    }

    private async Task CatchUpRoom(
        Guid locationId,
        InGameDate currentDate,
        CancellationToken cancellationToken
    )
    {
        var creatureIds = await getCreatureIdsWithJobInLocation.Handle(
            new GetCreatureIdsWithCreatureJobInLocationQuery { LocationId = locationId },
            cancellationToken
        );
        await CatchUp("Room", creatureIds, currentDate, cancellationToken);
    }

    private async Task CatchUpDistrict(
        Guid worldId,
        Guid districtId,
        InGameDate currentDate,
        CancellationToken cancellationToken
    )
    {
        var creatureIds = await getCreatureIdsByDistrict.Handle(
            new GetCreatureIdsByDistrictQuery { WorldId = worldId, DistrictId = districtId },
            cancellationToken
        );
        await CatchUp("District", creatureIds, currentDate, cancellationToken);
    }

    private async Task CatchUp(
        string scope,
        IReadOnlyCollection<Guid> creatureIds,
        InGameDate currentDate,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var workingCreaturesByLocationId = new Dictionary<Guid, List<Guid>>();

        foreach (var creatureId in creatureIds)
        {
            var jobs = await getAllJobsByCreatureId.Handle(
                new GetAllCreatureJobsByCreatureIdQuery { CreatureId = creatureId },
                cancellationToken
            );

            var dueJob = jobs.Where(j =>
                    CreatureJobScheduling.IsActiveAtHour(j, currentDate.Weekday, currentDate.Hour)
                )
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.Id)
                .FirstOrDefault();

            if (dueJob == null)
            {
                continue;
            }

            var creature = await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = creatureId },
                cancellationToken
            );

            if (creature != null)
            {
                await executeJob.Handle(
                    new ExecuteCreatureJobCommand
                    {
                        CreatureId = creature.Id,
                        CurrentLocationId = creature.LocationId,
                        CurrentState = creature.State,
                        CreatureJobAction = dueJob.Action,
                        JobLocationId = dueJob.LocationId,
                    },
                    cancellationToken
                );
            }

            if (dueJob.Action == CreatureJobAction.Work && dueJob.LocationId != null)
            {
                workingCreaturesByLocationId.TryAdd(dueJob.LocationId.Value, []);
                workingCreaturesByLocationId[dueJob.LocationId.Value].Add(creatureId);
            }
        }

        foreach (var (locationId, presentCreatureIds) in workingCreaturesByLocationId)
        {
            await AssignWorkstations(locationId, presentCreatureIds, cancellationToken);
        }

        stopwatch.Stop();

        logger.LogInformation(
            "[perf] CatchUp{Scope} processed {CreatureCount} people in {ElapsedMs}ms",
            scope,
            creatureIds.Count,
            stopwatch.ElapsedMilliseconds
        );
    }

    private async Task AssignWorkstations(
        Guid locationId,
        IReadOnlyList<Guid> presentCreatureIds,
        CancellationToken cancellationToken
    )
    {
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = locationId },
            cancellationToken
        );
        if (location?.RoomId == null)
        {
            return;
        }

        var workstations = await getWorkstationsByRoomId.Handle(
            new GetWorkstationsByRoomIdQuery { RoomId = location.RoomId.Value },
            cancellationToken
        );
        var counter = workstations.Where(w => w.WorkstationType == WorkstationType.Trade);
        var productionStations = workstations.Where(w =>
            w.WorkstationType != WorkstationType.Trade
        );
        var orderedStations = counter.Concat(productionStations).ToArray();

        var remainingCreatureIds = new Queue<Guid>(presentCreatureIds);
        foreach (var station in orderedStations)
        {
            var occupantId =
                remainingCreatureIds.Count > 0 ? remainingCreatureIds.Dequeue() : (Guid?)null;
            await setWorkstationOccupant.Handle(
                new SetWorkstationOccupantCommand
                {
                    WorkstationId = station.Id,
                    OccupantId = occupantId,
                },
                cancellationToken
            );
        }
    }

    private async Task SyncFrontDoorLock(
        Guid roomId,
        InGameDate currentDate,
        CancellationToken cancellationToken
    )
    {
        var roomSummary = await getRoomSummary.Handle(
            new GetRoomSummaryQuery { RoomId = roomId },
            cancellationToken
        );
        if (roomSummary == null || roomSummary.RoomFloorNumber != 0)
        {
            return;
        }

        await syncScheduleLock.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = roomSummary.BuildingId,
                BuildingType = roomSummary.BuildingType,
                CurrentDate = currentDate,
            },
            cancellationToken
        );
    }
}
