using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.WorldGeneration;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public record BuildingLockTarget(Guid BuildingId, BuildingType BuildingType);

public class SyncScheduleLocksCommand
{
    public required IReadOnlyCollection<BuildingLockTarget> Buildings { get; init; }
    public required InGameDate CurrentDate { get; init; }
}

internal class SyncScheduleLocksCommandHandler(
    IQueryHandler<
        GetBuildingOwnersByBuildingIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<BuildingOwner>>
    > getBuildingOwnersByBuildingIds,
    IQueryHandler<
        GetCreatureJobsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>>
    > getCreatureJobsByCreatureIds,
    IQueryHandler<
        GetCreatureJobsOfWorkersAtLocationsQuery,
        IReadOnlyList<CreatureJob>
    > getJobsOfBuildingWorkers,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetRoomsByBuildingIdsQuery, IReadOnlyCollection<Room>> getRoomsByBuildingIds,
    ICommandHandler<SetFrontDoorsLockedCommand> setFrontDoorsLocked
) : ICommandHandler<SyncScheduleLocksCommand>
{
    private static readonly HashSet<BuildingType> NeverLocked =
    [
        BuildingType.Inn,
        BuildingType.Tavern,
    ];

    public async Task Handle(
        SyncScheduleLocksCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var lockableBuildings = command
            .Buildings.Where(building => !NeverLocked.Contains(building.BuildingType))
            .ToArray();
        if (lockableBuildings.Length == 0)
        {
            return;
        }

        var shops = lockableBuildings
            .Where(building => ShopBuildingTypes.IsShop(building.BuildingType))
            .ToArray();
        var homes = lockableBuildings
            .Where(building => !ShopBuildingTypes.IsShop(building.BuildingType))
            .ToArray();

        await SyncShopLocks(shops, cancellationToken);
        await SyncHomeLocks(homes, command.CurrentDate, cancellationToken);
    }

    private async Task SyncShopLocks(
        IReadOnlyCollection<BuildingLockTarget> shops,
        CancellationToken cancellationToken
    )
    {
        if (shops.Count == 0)
        {
            return;
        }

        var shopIds = shops.Select(shop => shop.BuildingId).ToArray();
        var rooms = await getRoomsByBuildingIds.Handle(
            new GetRoomsByBuildingIdsQuery { BuildingIds = shopIds },
            cancellationToken
        );
        var roomLocationIdsByBuilding = rooms
            .GroupBy(room => room.BuildingId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(room => room.LocationId).ToHashSet()
            );

        var workerJobs = await getJobsOfBuildingWorkers.Handle(
            new GetCreatureJobsOfWorkersAtLocationsQuery
            {
                LocationIds = rooms.Select(room => room.LocationId).Distinct().ToArray(),
            },
            cancellationToken
        );
        var workersById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery
            {
                Ids = workerJobs.Select(job => job.CreatureId).Distinct().ToArray(),
            },
            cancellationToken
        );

        var workingLocationIds = workersById
            .Values.Where(worker => worker.Activity == CreatureActivity.Working)
            .Select(worker => worker.LocationId)
            .ToHashSet();

        var buildingIdsByLockState = shops
            .Select(shop =>
                (
                    shop.BuildingId,
                    IsLocked: !roomLocationIdsByBuilding
                        .GetValueOrDefault(shop.BuildingId, [])
                        .Overlaps(workingLocationIds)
                )
            )
            .GroupBy(entry => entry.IsLocked, entry => entry.BuildingId)
            .ToArray();

        foreach (var group in buildingIdsByLockState)
        {
            await setFrontDoorsLocked.Handle(
                new SetFrontDoorsLockedCommand
                {
                    BuildingIds = group.ToArray(),
                    IsLocked = group.Key,
                },
                cancellationToken
            );
        }
    }

    private async Task SyncHomeLocks(
        IReadOnlyCollection<BuildingLockTarget> homes,
        InGameDate currentDate,
        CancellationToken cancellationToken
    )
    {
        if (homes.Count == 0)
        {
            return;
        }

        var homeIds = homes.Select(home => home.BuildingId).ToArray();
        var ownersByBuilding = await getBuildingOwnersByBuildingIds.Handle(
            new GetBuildingOwnersByBuildingIdsQuery { BuildingIds = homeIds },
            cancellationToken
        );

        var ownerIdByBuildingId = homes
            .Select(home =>
                (
                    home.BuildingId,
                    OwnerId: ownersByBuilding
                        .GetValueOrDefault(home.BuildingId)
                        ?.FirstOrDefault()
                        ?.OwnerId
                )
            )
            .Where(entry => entry.OwnerId != null)
            .ToArray();
        if (ownerIdByBuildingId.Length == 0)
        {
            return;
        }

        var jobsByCreatureId = await getCreatureJobsByCreatureIds.Handle(
            new GetCreatureJobsByCreatureIdsQuery
            {
                CreatureIds = ownerIdByBuildingId
                    .Select(entry => entry.OwnerId!.Value)
                    .Distinct()
                    .ToArray(),
            },
            cancellationToken
        );

        var buildingIdsByLockState = ownerIdByBuildingId
            .Select(entry =>
                (
                    entry.BuildingId,
                    ActiveJob: jobsByCreatureId
                        .GetValueOrDefault(entry.OwnerId!.Value, [])
                        .Where(job =>
                            CreatureJobScheduling.IsActiveAtHour(
                                job,
                                currentDate.Weekday,
                                currentDate.Hour
                            )
                        )
                        .OrderByDescending(job => job.Priority)
                        .ThenBy(job => job.Id)
                        .FirstOrDefault()
                )
            )
            .Where(entry => entry.ActiveJob != null)
            .GroupBy(
                entry => entry.ActiveJob!.Action == CreatureJobAction.Sleep,
                entry => entry.BuildingId
            )
            .ToArray();

        foreach (var group in buildingIdsByLockState)
        {
            await setFrontDoorsLocked.Handle(
                new SetFrontDoorsLockedCommand
                {
                    BuildingIds = group.ToArray(),
                    IsLocked = group.Key,
                },
                cancellationToken
            );
        }
    }
}
