using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.LocationSimulation.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.Routing.Commands;
using TRPG.Application.Routing.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class CatchUpLocationCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required InGameDate CurrentDate { get; init; }
    public required int PlayerLevel { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class CatchUpLocationCommandHandler(
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<
        GetCreatureIdsWithCreatureJobInLocationQuery,
        IReadOnlyList<Guid>
    > getCreatureIdsWithJobInLocation,
    IQueryHandler<GetCreatureIdsByDistrictQuery, IReadOnlyList<Guid>> getCreatureIdsByDistrict,
    IQueryHandler<GetRouteIdsByLocationIdQuery, IReadOnlyList<Guid>> getRouteIdsByLocationId,
    IQueryHandler<
        GetCreatureIdsWithCreatureJobOnRoutesQuery,
        IReadOnlyList<Guid>
    > getCreatureIdsWithJobOnRoutes,
    ICommandHandler<
        SyncCreatureJobSchedulesCommand,
        SyncCreatureJobSchedulesResult
    > syncCreatureJobSchedules,
    IQueryHandler<
        GetWorkstationsByLocationIdQuery,
        IReadOnlyCollection<Workstation>
    > getWorkstationsByLocationId,
    ICommandHandler<SetWorkstationOccupantCommand> setWorkstationOccupant,
    ICommandHandler<SyncFrontDoorLockCommand> syncFrontDoorLock,
    ICommandHandler<SyncCreatureSpawnerCommand> syncCreatureSpawner,
    ICommandHandler<SyncRestockPolicyCommand> syncRestockPolicy,
    ICommandHandler<SyncQuestSeedScheduleCommand> syncQuestSeedSchedule,
    ICommandHandler<SyncWeatherCommand> syncWeather,
    IQueryHandler<GetWeatherByStateIdQuery, WeatherCondition?> getWeatherByStateId,
    ICommandHandler<SyncRouteTravelersCommand> syncRouteTravelers,
    ICommandHandler<EnsureCreatureRouteSchedulesCommand> ensureCreatureRouteSchedules,
    ICommandHandler<
        MaterializeScheduledRouteTravelersCommand,
        IReadOnlyCollection<Guid>
    > materializeScheduledRouteTravelers,
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
        await SynchronizeWeather(command, location, cancellationToken);
        var weather = await getWeatherByStateId.Handle(
            new GetWeatherByStateIdQuery { StateId = location.StateId },
            cancellationToken
        );

        await ensureCreatureRouteSchedules.Handle(
            new EnsureCreatureRouteSchedulesCommand { WorldId = command.WorldId },
            cancellationToken
        );
        var scheduledTravelerCreatureIds = await MaterializeScheduledRouteTravelers(
            command,
            cancellationToken
        );
        await SynchronizeRouteTravelers(command, cancellationToken);

        var scheduledCreatureIds = await ResolveScheduledCreatureIds(
            command,
            location,
            cancellationToken
        );
        scheduledCreatureIds.UnionWith(scheduledTravelerCreatureIds);
        await AdvanceDueJobs(scheduledCreatureIds, command.GameTime, weather, cancellationToken);

        await SynchronizeFrontDoorLock(command, cancellationToken);

        await SynchronizeCreatureSpawner(command, cancellationToken);
        await SynchronizeRestockPolicy(command, cancellationToken);
        await SynchronizeQuestSeedSchedule(command, cancellationToken);
    }

    // Creatures whose job targets this location and creatures already standing in its district
    // overlap, so they are unioned to avoid advancing the same creature's schedule twice.
    private async Task<HashSet<Guid>> ResolveScheduledCreatureIds(
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

        var routeIds = await getRouteIdsByLocationId.Handle(
            new GetRouteIdsByLocationIdQuery
            {
                WorldId = command.WorldId,
                LocationId = command.LocationId,
            },
            cancellationToken
        );
        creatureIds.UnionWith(
            await getCreatureIdsWithJobOnRoutes.Handle(
                new GetCreatureIdsWithCreatureJobOnRoutesQuery { RouteIds = routeIds },
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

    private async Task<IReadOnlyCollection<Guid>> MaterializeScheduledRouteTravelers(
        CatchUpLocationCommand command,
        CancellationToken cancellationToken
    ) =>
        await materializeScheduledRouteTravelers.Handle(
            new MaterializeScheduledRouteTravelersCommand
            {
                WorldId = command.WorldId,
                LocationId = command.LocationId,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

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
                CurrentGameTime = command.GameTime,
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
                CurrentGameTime = command.GameTime,
            },
            cancellationToken
        );
    }

    private async Task SynchronizeQuestSeedSchedule(
        CatchUpLocationCommand command,
        CancellationToken cancellationToken
    )
    {
        await syncQuestSeedSchedule.Handle(
            new SyncQuestSeedScheduleCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
                PlayerLevel = command.PlayerLevel,
                CurrentGameTime = command.GameTime,
            },
            cancellationToken
        );
    }

    private async Task SynchronizeWeather(
        CatchUpLocationCommand command,
        Location location,
        CancellationToken cancellationToken
    )
    {
        await syncWeather.Handle(
            new SyncWeatherCommand
            {
                WorldId = command.WorldId,
                StateId = location.StateId,
                CurrentGameTime = command.GameTime,
            },
            cancellationToken
        );
    }

    private async Task SynchronizeRouteTravelers(
        CatchUpLocationCommand command,
        CancellationToken cancellationToken
    )
    {
        await syncRouteTravelers.Handle(
            new SyncRouteTravelersCommand
            {
                WorldId = command.WorldId,
                LocationId = command.LocationId,
                GameTime = command.GameTime,
            },
            cancellationToken
        );
    }

    private async Task AdvanceDueJobs(
        IReadOnlyCollection<Guid> creatureIds,
        GameInstant gameTime,
        WeatherCondition? weather,
        CancellationToken cancellationToken
    )
    {
        if (creatureIds.Count == 0)
        {
            return;
        }

        var result = await syncCreatureJobSchedules.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = creatureIds,
                GameTime = gameTime,
                Weather = weather,
            },
            cancellationToken
        );
        foreach (var (locationId, presentCreatureIds) in result.WorkingCreatureIdsByLocationId)
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
