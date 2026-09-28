using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Queries;

public class GetMovementPlaceLabelsQuery
{
    public required Guid PlayerId { get; init; }
    public required IReadOnlyCollection<Guid> ArrivedCreatureIds { get; init; }
    public required IReadOnlyCollection<Guid> DepartedCreatureIds { get; init; }
}

internal class GetMovementPlaceLabelsQueryHandler(
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<
        GetConnectorsByLocationIdQuery,
        IReadOnlyCollection<LocationConnector>
    > getConnectorsByLocationId,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<GetDistrictsByIdsQuery, IReadOnlyDictionary<Guid, District>> getDistrictsByIds,
    IQueryHandler<GetRoomsByIdsQuery, IReadOnlyDictionary<Guid, Room>> getRoomsByIds,
    IQueryHandler<GetBuildingsByIdsQuery, IReadOnlyDictionary<Guid, Building>> getBuildingsByIds
) : IQueryHandler<GetMovementPlaceLabelsQuery, IReadOnlyDictionary<Guid, string>>
{
    public async Task<IReadOnlyDictionary<Guid, string>> Handle(
        GetMovementPlaceLabelsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creatureIds = query
            .ArrivedCreatureIds.Concat(query.DepartedCreatureIds)
            .Append(query.PlayerId)
            .Distinct()
            .ToArray();
        var creatures = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = creatureIds },
            cancellationToken
        );
        var playerLocationId = creatures[query.PlayerId].LocationId;

        var otherLocationIdByCreatureId = ResolveOtherLocationIds(
            query,
            creatures,
            playerLocationId
        );
        if (otherLocationIdByCreatureId.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var otherLocationIds = otherLocationIdByCreatureId.Values.Distinct().ToArray();
        var namesByLocationId = await ResolvePlaceNames(
            playerLocationId,
            otherLocationIds,
            cancellationToken
        );

        return otherLocationIdByCreatureId
            .Where(entry => namesByLocationId.ContainsKey(entry.Value))
            .ToDictionary(entry => entry.Key, entry => namesByLocationId[entry.Value]);
    }

    private static Dictionary<Guid, Guid> ResolveOtherLocationIds(
        GetMovementPlaceLabelsQuery query,
        IReadOnlyDictionary<Guid, Creature> creatures,
        Guid playerLocationId
    )
    {
        var otherLocationIds = new Dictionary<Guid, Guid>();
        foreach (var creatureId in query.ArrivedCreatureIds)
        {
            if (
                creatures.TryGetValue(creatureId, out var creature)
                && creature.PreviousLocationId is { } originLocationId
                && originLocationId != playerLocationId
            )
            {
                otherLocationIds[creatureId] = originLocationId;
            }
        }

        foreach (var creatureId in query.DepartedCreatureIds)
        {
            if (
                creatures.TryGetValue(creatureId, out var creature)
                && creature.LocationId != playerLocationId
            )
            {
                otherLocationIds[creatureId] = creature.LocationId;
            }
        }

        return otherLocationIds;
    }

    // The label the player already sees on an exit is the name they'd recognize the place by.
    private async Task<IReadOnlyDictionary<Guid, string>> ResolvePlaceNames(
        Guid playerLocationId,
        IReadOnlyCollection<Guid> locationIds,
        CancellationToken cancellationToken
    )
    {
        var connectors = await getConnectorsByLocationId.Handle(
            new GetConnectorsByLocationIdQuery { LocationId = playerLocationId },
            cancellationToken
        );
        var names = connectors
            .Where(connector => locationIds.Contains(connector.DestinationLocationId))
            .DistinctBy(connector => connector.DestinationLocationId)
            .ToDictionary(
                connector => connector.DestinationLocationId,
                connector => connector.DestinationLabel
            );

        var unlabeledLocationIds = locationIds.Where(id => !names.ContainsKey(id)).ToArray();
        var fallbackNames = await ResolveFallbackNames(unlabeledLocationIds, cancellationToken);
        foreach (var (locationId, name) in fallbackNames)
        {
            names[locationId] = name;
        }

        return names;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveFallbackNames(
        IReadOnlyCollection<Guid> locationIds,
        CancellationToken cancellationToken
    )
    {
        if (locationIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var locations = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = locationIds },
            cancellationToken
        );
        var districtIds = locations
            .Values.Select(location => location.DistrictId)
            .OfType<Guid>()
            .ToArray();
        var districts = await getDistrictsByIds.Handle(
            new GetDistrictsByIdsQuery { Ids = districtIds },
            cancellationToken
        );
        var roomIds = locations.Values.Select(location => location.RoomId).OfType<Guid>().ToArray();
        var rooms = await getRoomsByIds.Handle(
            new GetRoomsByIdsQuery { Ids = roomIds },
            cancellationToken
        );
        var buildingIds = rooms.Values.Select(room => room.BuildingId).Distinct().ToArray();
        var buildings = await getBuildingsByIds.Handle(
            new GetBuildingsByIdsQuery { Ids = buildingIds },
            cancellationToken
        );

        return locations.ToDictionary(
            entry => entry.Key,
            entry =>
                entry.Value.Kind switch
                {
                    LocationKind.Room => buildings[
                        rooms[entry.Value.RoomId!.Value].BuildingId
                    ].Name,
                    LocationKind.District => districts[entry.Value.DistrictId!.Value].Name,
                    _ => entry.Value.Name,
                }
        );
    }
}
