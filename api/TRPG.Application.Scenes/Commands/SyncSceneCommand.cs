using TRPG.Application.Buildings.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Commands;

public class SyncSceneCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required InGameDate CurrentDate { get; init; }
}

internal class SyncSceneCommandHandler(
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<
        GetCreatureIdsWithCreatureJobInLocationQuery,
        IReadOnlyList<Guid>
    > getCreatureIdsWithJobInLocation,
    IQueryHandler<
        GetAllCreatureJobsByCreatureIdQuery,
        IReadOnlyList<CreatureJob>
    > getAllJobsByCreatureId,
    IQueryHandler<GetCreatureIdsByDistrictQuery, IReadOnlyList<Guid>> getCreatureIdsByDistrict,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<ExecuteCreatureJobCommand> executeJob,
    IQueryHandler<
        GetWorkstationsByLocationIdQuery,
        IReadOnlyCollection<Workstation>
    > getWorkstationsByLocationId,
    ICommandHandler<SetWorkstationOccupantCommand> setWorkstationOccupant,
    ICommandHandler<SyncFrontDoorLockCommand> syncFrontDoorLock
) : ICommandHandler<SyncSceneCommand>
{
    public async Task Handle(
        SyncSceneCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.LocationId },
            cancellationToken
        );
        if (location == null)
        {
            return;
        }

        await AdvanceJobsTargetingLocation(
            command.LocationId,
            command.CurrentDate,
            cancellationToken
        );

        if (location.RoomId != null)
        {
            await syncFrontDoorLock.Handle(
                new SyncFrontDoorLockCommand
                {
                    LocationId = command.LocationId,
                    CurrentDate = command.CurrentDate,
                },
                cancellationToken
            );
        }

        if (location.DistrictId != null)
        {
            await AdvanceJobsForCreaturesInDistrict(
                command.WorldId,
                location.DistrictId.Value,
                command.CurrentDate,
                cancellationToken
            );
        }
    }

    private async Task AdvanceJobsTargetingLocation(
        Guid locationId,
        InGameDate currentDate,
        CancellationToken cancellationToken
    )
    {
        var creatureIds = await getCreatureIdsWithJobInLocation.Handle(
            new GetCreatureIdsWithCreatureJobInLocationQuery { LocationId = locationId },
            cancellationToken
        );
        await AdvanceDueJobs(creatureIds, currentDate, cancellationToken);
    }

    private async Task AdvanceJobsForCreaturesInDistrict(
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
        await AdvanceDueJobs(creatureIds, currentDate, cancellationToken);
    }

    private async Task AdvanceDueJobs(
        IReadOnlyCollection<Guid> creatureIds,
        InGameDate currentDate,
        CancellationToken cancellationToken
    )
    {
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

            if (dueJob.Action == CreatureJobAction.Work)
            {
                workingCreaturesByLocationId.TryAdd(dueJob.LocationId, []);
                workingCreaturesByLocationId[dueJob.LocationId].Add(creatureId);
            }
        }

        foreach (var (locationId, presentCreatureIds) in workingCreaturesByLocationId)
        {
            await AssignWorkstations(locationId, presentCreatureIds, cancellationToken);
        }
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

        var workstations = await getWorkstationsByLocationId.Handle(
            new GetWorkstationsByLocationIdQuery { LocationId = locationId },
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
}
