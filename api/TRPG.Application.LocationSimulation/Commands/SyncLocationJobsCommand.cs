using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.LocationSimulation.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.Routing.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncLocationJobsCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class SyncLocationJobsCommandHandler(
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<
        GetCreatureIdsWithCreatureJobInLocationsQuery,
        IReadOnlyList<Guid>
    > getCreatureIdsWithJobInLocations,
    IQueryHandler<
        GetBuildingsByLocationQuery,
        IReadOnlyCollection<Building>
    > getBuildingsByLocation,
    IQueryHandler<GetRoomsByBuildingIdsQuery, IReadOnlyCollection<Room>> getRoomsByBuildingIds,
    IQueryHandler<GetCreatureIdsByDistrictQuery, IReadOnlyList<Guid>> getCreatureIdsByDistrict,
    IQueryHandler<GetRouteIdsByLocationIdQuery, IReadOnlyList<Guid>> getRouteIdsByLocationId,
    IQueryHandler<
        GetCreatureIdsWithCreatureJobOnRoutesQuery,
        IReadOnlyList<Guid>
    > getCreatureIdsWithJobOnRoutes,
    IQueryHandler<
        GetTravelerCreatureIdsByLocationIdQuery,
        IReadOnlyList<Guid>
    > getTravelerCreatureIdsByLocationId,
    IQueryHandler<GetWeatherByStateIdQuery, WeatherCondition?> getWeatherByStateId,
    ICommandHandler<
        SyncCreatureJobSchedulesCommand,
        SyncCreatureJobSchedulesResult
    > syncCreatureJobSchedules,
    IQueryHandler<
        GetWorkstationsByLocationIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Workstation>>
    > getWorkstationsByLocationIds,
    ICommandHandler<SetWorkstationOccupantsCommand> setWorkstationOccupants
) : ICommandHandler<SyncLocationJobsCommand>
{
    public async Task Handle(
        SyncLocationJobsCommand command,
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

        var creatureIds = await ResolveScheduledCreatureIds(command, location, cancellationToken);
        if (creatureIds.Count == 0)
        {
            return;
        }

        var weather = await getWeatherByStateId.Handle(
            new GetWeatherByStateIdQuery { StateId = location.StateId },
            cancellationToken
        );
        var result = await syncCreatureJobSchedules.Handle(
            new SyncCreatureJobSchedulesCommand
            {
                CreatureIds = creatureIds,
                GameTime = command.GameTime,
                Weather = weather,
            },
            cancellationToken
        );
        await AssignWorkstations(result.WorkingCreatureIdsByLocationId, cancellationToken);
    }

    // The sources overlap, so they are unioned to avoid advancing a schedule twice.
    private async Task<HashSet<Guid>> ResolveScheduledCreatureIds(
        SyncLocationJobsCommand command,
        Location location,
        CancellationToken cancellationToken
    )
    {
        var creatureIds = new HashSet<Guid>(
            await getCreatureIdsWithJobInLocations.Handle(
                new GetCreatureIdsWithCreatureJobInLocationsQuery
                {
                    LocationIds = await ResolveJobLocationIds(
                        command.LocationId,
                        cancellationToken
                    ),
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

        creatureIds.UnionWith(
            await getTravelerCreatureIdsByLocationId.Handle(
                new GetTravelerCreatureIdsByLocationIdQuery
                {
                    WorldId = command.WorldId,
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

    private async Task<IReadOnlyCollection<Guid>> ResolveJobLocationIds(
        Guid locationId,
        CancellationToken cancellationToken
    )
    {
        var buildings = await getBuildingsByLocation.Handle(
            new GetBuildingsByLocationQuery { LocationId = locationId },
            cancellationToken
        );
        if (buildings.Count == 0)
        {
            return [locationId];
        }

        var rooms = await getRoomsByBuildingIds.Handle(
            new GetRoomsByBuildingIdsQuery
            {
                BuildingIds = buildings.Select(building => building.Id).ToArray(),
            },
            cancellationToken
        );
        return [locationId, .. rooms.Select(room => room.LocationId)];
    }

    private async Task AssignWorkstations(
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> presentCreatureIdsByLocationId,
        CancellationToken cancellationToken
    )
    {
        if (presentCreatureIdsByLocationId.Count == 0)
        {
            return;
        }

        var locationIds = presentCreatureIdsByLocationId.Keys.ToArray();
        var locationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = locationIds },
            cancellationToken
        );
        var roomLocationIds = locationsById
            .Where(kv => kv.Value.RoomId != null)
            .Select(kv => kv.Key)
            .ToArray();
        if (roomLocationIds.Length == 0)
        {
            return;
        }

        var workstationsByLocationId = await getWorkstationsByLocationIds.Handle(
            new GetWorkstationsByLocationIdsQuery { LocationIds = roomLocationIds },
            cancellationToken
        );

        var occupantIdsByWorkstationId = new Dictionary<Guid, Guid?>();
        foreach (var locationId in roomLocationIds)
        {
            var workstations = workstationsByLocationId.GetValueOrDefault(
                locationId,
                (IReadOnlyList<Workstation>)[]
            );
            var counter = workstations.Where(w => w.WorkstationType == WorkstationType.Trade);
            var productionStations = workstations.Where(w =>
                w.WorkstationType != WorkstationType.Trade
            );
            var orderedStations = counter.Concat(productionStations).ToArray();

            var remainingCreatureIds = new Queue<Guid>(presentCreatureIdsByLocationId[locationId]);
            foreach (var station in orderedStations)
            {
                occupantIdsByWorkstationId[station.Id] =
                    remainingCreatureIds.Count > 0 ? remainingCreatureIds.Dequeue() : (Guid?)null;
            }
        }

        await setWorkstationOccupants.Handle(
            new SetWorkstationOccupantsCommand
            {
                OccupantIdsByWorkstationId = occupantIdsByWorkstationId,
            },
            cancellationToken
        );
    }
}
