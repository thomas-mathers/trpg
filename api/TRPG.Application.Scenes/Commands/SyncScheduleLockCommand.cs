using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.WorldGeneration;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Commands;

public class SyncScheduleLockCommand
{
    public required Guid BuildingId { get; init; }
    public required BuildingType BuildingType { get; init; }
    public required InGameDate CurrentDate { get; init; }
}

internal class SyncScheduleLockCommandHandler(
    IQueryHandler<
        GetBuildingOwnersByBuildingIdQuery,
        IReadOnlyCollection<BuildingOwner>
    > getAllBuildingOwnersByBuildingId,
    IQueryHandler<
        GetCreatureJobsByCreatureIdQuery,
        IReadOnlyList<CreatureJob>
    > getAllJobsByCreatureId,
    IQueryHandler<
        GetCreatureJobsOfBuildingWorkersQuery,
        IReadOnlyList<CreatureJob>
    > getJobsOfBuildingWorkers,
    ICommandHandler<SetFrontDoorLockedCommand, bool?> setFrontDoorLocked
) : ICommandHandler<SyncScheduleLockCommand, bool?>
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

        if (ShopBuildingTypes.IsShop(command.BuildingType))
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
            new GetCreatureJobsOfBuildingWorkersQuery { BuildingId = command.BuildingId },
            cancellationToken
        );

        var anyoneWorking = workerJobs
            .GroupBy(j => j.CreatureId)
            .Select(creatureJobs =>
                creatureJobs
                    .Where(j =>
                        CreatureJobScheduling.IsActiveAtHour(
                            j,
                            command.CurrentDate.Weekday,
                            command.CurrentDate.Hour
                        )
                    )
                    .OrderByDescending(j => j.Priority)
                    .ThenBy(j => j.Id)
                    .FirstOrDefault()
            )
            .Any(effectiveJob => effectiveJob is { Action: CreatureJobAction.Work });

        return await setFrontDoorLocked.Handle(
            new SetFrontDoorLockedCommand
            {
                BuildingId = command.BuildingId,
                IsLocked = !anyoneWorking,
            },
            cancellationToken
        );
    }

    private async Task<bool?> SyncHomeLock(
        SyncScheduleLockCommand command,
        CancellationToken cancellationToken
    )
    {
        var owners = await getAllBuildingOwnersByBuildingId.Handle(
            new GetBuildingOwnersByBuildingIdQuery { BuildingId = command.BuildingId },
            cancellationToken
        );
        var ownerId = owners.FirstOrDefault()?.OwnerId;
        if (ownerId == null)
        {
            return null;
        }

        var jobs = await getAllJobsByCreatureId.Handle(
            new GetCreatureJobsByCreatureIdQuery { CreatureId = ownerId.Value },
            cancellationToken
        );
        var activeJob = jobs.Where(j =>
                CreatureJobScheduling.IsActiveAtHour(
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
                IsLocked = activeJob.Action == CreatureJobAction.Sleep,
            },
            cancellationToken
        );
    }
}
