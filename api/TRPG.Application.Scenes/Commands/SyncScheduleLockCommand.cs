using TRPG.Application.Buildings;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Common.Handling;
using TRPG.Application.CreatureJobs;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;

namespace TRPG.Application.Scenes.Commands;

public class SyncScheduleLockCommand
{
    public required Guid BuildingId { get; init; }
    public required BuildingType BuildingType { get; init; }
    public required InGameDate CurrentDate { get; init; }
}

internal class SyncScheduleLockCommandHandler(
    IQueryHandler<
        GetAllOwnersByBuildingIdQuery,
        IReadOnlyCollection<BuildingOwner>
    > getAllOwnersByBuildingId,
    IQueryHandler<
        GetAllCreatureJobsByCreatureIdQuery,
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
            new GetAllCreatureJobsByCreatureIdQuery { CreatureId = ownerId.Value },
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
