using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Jobs;
using TRPG.Application.Jobs.Commands;
using TRPG.Application.Jobs.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Scenes.Commands;

internal class SyncCommand
{
    public required Guid WorldId { get; init; }
    public required Guid? RoomId { get; init; }
    public required Guid? DistrictId { get; init; }
    public required InGameDate CurrentDate { get; init; }
}

internal class SyncCommandHandler(
    GetCreatureIdsWithJobInRoomQueryHandler getCreatureIdsWithJobInRoom,
    GetAllJobsByCreatureIdQueryHandler getAllJobsByCreatureId,
    GetCreatureIdsByDistrictQueryHandler getCreatureIdsByDistrict,
    GetCreatureByIdQueryHandler getCreatureById,
    ExecuteJobCommandHandler executeJob,
    GetWorkstationsByRoomIdQueryHandler getWorkstationsByRoomId,
    SetWorkstationOccupantCommandHandler setWorkstationOccupant,
    GetRoomSummaryQueryHandler getRoomSummary,
    SyncScheduleLockCommandHandler syncScheduleLock,
    ILogger<SyncCommandHandler> logger
)
{
    public async Task Handle(SyncCommand command, CancellationToken cancellationToken = default)
    {
        if (command.RoomId != null)
        {
            await CatchUpRoom(command.RoomId.Value, command.CurrentDate, cancellationToken);
            await SyncFrontDoorLock(command.RoomId.Value, command.CurrentDate, cancellationToken);
        }
        else if (command.DistrictId != null)
        {
            await CatchUpDistrict(
                command.WorldId,
                command.DistrictId.Value,
                command.CurrentDate,
                cancellationToken
            );
        }
    }

    private async Task CatchUpRoom(
        Guid roomId,
        InGameDate currentDate,
        CancellationToken cancellationToken
    )
    {
        var creatureIds = await getCreatureIdsWithJobInRoom.Handle(
            new GetCreatureIdsWithJobInRoomQuery { RoomId = roomId },
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
        var workingCreaturesByRoomId = new Dictionary<Guid, List<Guid>>();

        foreach (var creatureId in creatureIds)
        {
            var jobs = await getAllJobsByCreatureId.Handle(
                new GetAllJobsByCreatureIdQuery { CreatureId = creatureId },
                cancellationToken
            );

            var dueJob = jobs.Where(j =>
                    JobScheduling.IsActiveAtHour(j, currentDate.Weekday, currentDate.Hour)
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
                    new ExecuteJobCommand
                    {
                        CreatureId = creature.Id,
                        CurrentRoomId = creature.RoomId,
                        CurrentState = creature.State,
                        JobAction = dueJob.Action,
                        JobRoomId = dueJob.RoomId,
                    },
                    cancellationToken
                );
            }

            if (dueJob.Action == JobAction.Work && dueJob.RoomId != null)
            {
                workingCreaturesByRoomId.TryAdd(dueJob.RoomId.Value, []);
                workingCreaturesByRoomId[dueJob.RoomId.Value].Add(creatureId);
            }
        }

        foreach (var (roomId, presentCreatureIds) in workingCreaturesByRoomId)
        {
            await AssignWorkstations(roomId, presentCreatureIds, cancellationToken);
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
        Guid roomId,
        IReadOnlyList<Guid> presentCreatureIds,
        CancellationToken cancellationToken
    )
    {
        var workstations = await getWorkstationsByRoomId.Handle(
            new GetWorkstationsByRoomIdQuery { RoomId = roomId },
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
