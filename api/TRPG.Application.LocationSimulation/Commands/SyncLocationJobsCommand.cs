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
        GetWorkstationsByLocationIdQuery,
        IReadOnlyCollection<Workstation>
    > getWorkstationsByLocationId,
    ICommandHandler<SetWorkstationOccupantCommand> setWorkstationOccupant
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
        foreach (var (locationId, presentCreatureIds) in result.WorkingCreatureIdsByLocationId)
        {
            await AssignWorkstations(locationId, presentCreatureIds, cancellationToken);
        }
    }

    // Creatures whose job targets this location, creatures already standing in its district, and
    // travelers passing through overlap, so they are unioned to avoid advancing a schedule twice.
    private async Task<HashSet<Guid>> ResolveScheduledCreatureIds(
        SyncLocationJobsCommand command,
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
