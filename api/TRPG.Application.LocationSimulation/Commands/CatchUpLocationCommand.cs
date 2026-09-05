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
        GetCreatureJobsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>>
    > getJobsByCreatureIds,
    IQueryHandler<GetCreatureIdsByDistrictQuery, IReadOnlyList<Guid>> getCreatureIdsByDistrict,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
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
        if (!catchUpCache.TryClaim(command.WorldId, command.LocationId, command.CurrentDate))
        {
            return false;
        }

        try
        {
            var location = await getLocationById.Handle(
                new GetLocationByIdQuery { Id = command.LocationId },
                cancellationToken
            );
            if (location == null)
            {
                catchUpCache.Evict(command.WorldId, command.LocationId, command.CurrentDate);
                return false;
            }

            await CatchUpLocation(command, location, cancellationToken);

            return true;
        }
        catch
        {
            catchUpCache.Evict(command.WorldId, command.LocationId, command.CurrentDate);
            throw;
        }
    }

    private async Task CatchUpLocation(
        CatchUpLocationCommand command,
        Location location,
        CancellationToken cancellationToken
    )
    {
        await AdvanceDueJobs(
            await ResolveScheduledCreatureIds(command, location, cancellationToken),
            command.CurrentDate,
            cancellationToken
        );

        if (location.RoomId != null)
        {
            await SynchronizeFrontDoorLock(command, cancellationToken);
        }

        await SynchronizeCreatureSpawner(command, cancellationToken);
        await SynchronizeRestockPolicy(command, cancellationToken);
    }

    // Creatures whose job targets this location and creatures already standing in its district
    // overlap, so they are unioned to avoid advancing the same creature's schedule twice.
    private async Task<IReadOnlyCollection<Guid>> ResolveScheduledCreatureIds(
        CatchUpLocationCommand command,
        Location location,
        CancellationToken cancellationToken
    )
    {
        var creatureIds = new HashSet<Guid>(
            await getCreatureIdsWithJobInLocation.Handle(
                new GetCreatureIdsWithCreatureJobInLocationQuery
                {
                    LocationId = command.LocationId,
                },
                cancellationToken
            )
        );

        if (location.DistrictId != null)
        {
            creatureIds.UnionWith(
                await getCreatureIdsByDistrict.Handle(
                    new GetCreatureIdsByDistrictQuery
                    {
                        WorldId = command.WorldId,
                        DistrictId = location.DistrictId.Value,
                    },
                    cancellationToken
                )
            );
        }

        return creatureIds;
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

    private async Task AdvanceDueJobs(
        IReadOnlyCollection<Guid> creatureIds,
        InGameDate currentDate,
        CancellationToken cancellationToken
    )
    {
        if (creatureIds.Count == 0)
        {
            return;
        }

        var jobsByCreatureId = await getJobsByCreatureIds.Handle(
            new GetCreatureJobsByCreatureIdsQuery { CreatureIds = creatureIds },
            cancellationToken
        );

        var dueJobsByCreatureId = creatureIds
            .Select(creatureId => new
            {
                CreatureId = creatureId,
                DueJob = FindDueJob(jobsByCreatureId, creatureId, currentDate),
            })
            .Where(entry => entry.DueJob != null)
            .ToDictionary(entry => entry.CreatureId, entry => entry.DueJob!);

        if (dueJobsByCreatureId.Count == 0)
        {
            return;
        }

        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = dueJobsByCreatureId.Keys.ToArray() },
            cancellationToken
        );

        var workingCreaturesByLocationId = new Dictionary<Guid, List<Guid>>();

        foreach (var (creatureId, dueJob) in dueJobsByCreatureId)
        {
            if (creaturesById.TryGetValue(creatureId, out var creature))
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

    private static CreatureJob? FindDueJob(
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>> jobsByCreatureId,
        Guid creatureId,
        InGameDate currentDate
    ) =>
        jobsByCreatureId.TryGetValue(creatureId, out var jobs)
            ? jobs.Where(job =>
                    CreatureJobScheduling.IsActiveAtHour(job, currentDate.Weekday, currentDate.Hour)
                )
                .OrderByDescending(job => job.Priority)
                .ThenBy(job => job.Id)
                .FirstOrDefault()
            : null;

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
