using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Jobs;
using TRPG.Application.Jobs.Queries;
using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;

namespace TRPG.Application.Scenes.Commands;

internal class SyncScheduleLockCommand
{
    public required Guid BuildingId { get; init; }
    public required BuildingType BuildingType { get; init; }
    public required InGameDate CurrentDate { get; init; }
}

internal class SyncScheduleLockCommandHandler(
    GetAllOwnersByBuildingIdQueryHandler getAllOwnersByBuildingId,
    GetAllJobsByCreatureIdQueryHandler getAllJobsByCreatureId,
    GetJobsOfBuildingWorkersQueryHandler getJobsOfBuildingWorkers,
    SetFrontDoorLockedCommandHandler setFrontDoorLocked
)
{
    private static readonly HashSet<BuildingType> NeverLocked =
    [
        BuildingType.Inn,
        BuildingType.Tavern,
    ];

    public async Task<bool?> Handle(
        SyncScheduleLockCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (NeverLocked.Contains(command.BuildingType))
        {
            return null;
        }

        if (ShopStaffingPolicy.StandardBuildingTypes.Contains(command.BuildingType))
        {
            return await SyncShopLock(command, cancellationToken);
        }

        return await SyncHomeLock(command, cancellationToken);
    }

    private async Task<bool?> SyncShopLock(
        SyncScheduleLockCommand command,
        CancellationToken cancellationToken
    )
    {
        var workerJobs = await getJobsOfBuildingWorkers.Handle(
            new GetJobsOfBuildingWorkersQuery { BuildingId = command.BuildingId },
            cancellationToken
        );

        var anyoneWorking = workerJobs
            .GroupBy(j => j.CreatureId)
            .Select(creatureJobs =>
                creatureJobs
                    .Where(j =>
                        JobScheduling.IsActiveAtHour(
                            j,
                            command.CurrentDate.Weekday,
                            command.CurrentDate.Hour
                        )
                    )
                    .OrderByDescending(j => j.Priority)
                    .ThenBy(j => j.Id)
                    .FirstOrDefault()
            )
            .Any(effectiveJob => effectiveJob is { Action: JobAction.Work });

        return await setFrontDoorLocked.Handle(
            new SetFrontDoorLockedCommand
            {
                BuildingId = command.BuildingId,
                IsLocked = !anyoneWorking,
            },
            cancellationToken
        );
    }

    // Homes lock while the owner sleeps — residents have keys, visitors knock during waking hours.
    private async Task<bool?> SyncHomeLock(
        SyncScheduleLockCommand command,
        CancellationToken cancellationToken
    )
    {
        var owners = await getAllOwnersByBuildingId.Handle(
            new GetAllOwnersByBuildingIdQuery { BuildingId = command.BuildingId },
            cancellationToken
        );
        var ownerId = owners.FirstOrDefault()?.OwnerId;
        if (ownerId == null)
        {
            return null;
        }

        var jobs = await getAllJobsByCreatureId.Handle(
            new GetAllJobsByCreatureIdQuery { CreatureId = ownerId.Value },
            cancellationToken
        );
        var activeJob = jobs.Where(j =>
                JobScheduling.IsActiveAtHour(
                    j,
                    command.CurrentDate.Weekday,
                    command.CurrentDate.Hour
                )
            )
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.Id)
            .FirstOrDefault();
        if (activeJob == null)
        {
            return null;
        }

        return await setFrontDoorLocked.Handle(
            new SetFrontDoorLockedCommand
            {
                BuildingId = command.BuildingId,
                IsLocked = activeJob.Action == JobAction.Sleep,
            },
            cancellationToken
        );
    }
}
