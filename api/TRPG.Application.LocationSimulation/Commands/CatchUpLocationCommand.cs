using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class CatchUpLocationCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required InGameDate CurrentDate { get; init; }
    public required int PlayerLevel { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class CatchUpLocationCommandHandler(
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<
        GetCreatureIdsWithCreatureJobInLocationQuery,
        IReadOnlyList<Guid>
    > getCreatureIdsWithJobInLocation,
    IQueryHandler<
        GetCreatureJobsByCreatureIdQuery,
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
    ICommandHandler<SyncFrontDoorLockCommand> syncFrontDoorLock,
    ICommandHandler<SyncCreatureSpawnerCommand> syncCreatureSpawner,
    ICommandHandler<SyncRestockPolicyCommand> syncRestockPolicy,
    LocationCatchUpCache catchUpCache
) : ICommandHandler<CatchUpLocationCommand, bool>
{
    public async Task<bool> Handle(
        CatchUpLocationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (catchUpCache.HasCaughtUp(command.WorldId, command.LocationId, command.CurrentDate.Hour))
        {
            return false;
        }

        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.LocationId },
            cancellationToken
        );
        if (location == null)
        {
            return false;
        }

        await CatchUpLocation(command, location, cancellationToken);

        catchUpCache.MarkCaughtUp(command.WorldId, command.LocationId, command.CurrentDate.Hour);

        return true;
    }

    private async Task CatchUpLocation(
        CatchUpLocationCommand command,
        Location location,
        CancellationToken cancellationToken
    )
    {
        await AdvanceJobsTargetingLocation(
            command.LocationId,
            command.CurrentDate,
            cancellationToken
        );

        if (location.RoomId != null)
        {
            await SynchronizeFrontDoorLock(command, cancellationToken);
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

        await SynchronizeCreatureSpawner(command, cancellationToken);
        await SynchronizeRestockPolicy(command, cancellationToken);
    }

    private async Task SynchronizeFrontDoorLock(
        CatchUpLocationCommand command,
        CancellationToken cancellationToken
    )
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

    private async Task SynchronizeCreatureSpawner(
        CatchUpLocationCommand command,
        CancellationToken cancellationToken
    )
    {
        await syncCreatureSpawner.Handle(
            new SyncCreatureSpawnerCommand
            {
                LocationId = command.LocationId,
                PlayerLevel = command.PlayerLevel,
                CurrentPlaytime = command.Playtime,
            },
            cancellationToken
        );
    }

    private async Task SynchronizeRestockPolicy(
        CatchUpLocationCommand command,
        CancellationToken cancellationToken
    )
    {
        await syncRestockPolicy.Handle(
            new SyncRestockPolicyCommand
            {
                LocationId = command.LocationId,
                PlayerLevel = command.PlayerLevel,
                CurrentPlaytime = command.Playtime,
            },
            cancellationToken
        );
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
                new GetCreatureJobsByCreatureIdQuery { CreatureId = creatureId },
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
