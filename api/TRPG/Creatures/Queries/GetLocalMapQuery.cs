using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Knowledge.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Queries;

public class GetLocalMapQuery
{
    public required Guid PlayerId { get; init; }
}

public record LocalMapRoom(
    Guid Id,
    Guid LocationId,
    string Name,
    int FloorNumber,
    Rectangle? Bounds,
    RoomRole? Role,
    bool IsVisited,
    bool IsFrontier,
    IReadOnlyList<LocalMapMarker> Markers
);

public record LocalMapPassage(
    Guid Id,
    Guid OriginRoomId,
    Guid DestinationRoomId,
    Polyline? Path,
    LocalMapLockKind LockKind
)
{
    public bool IsLocked => LockKind != LocalMapLockKind.None;
}

public record LocalMapResult(
    Guid BuildingId,
    string BuildingName,
    BuildingType BuildingType,
    Guid CurrentRoomId,
    IReadOnlyList<LocalMapRoom> Rooms,
    IReadOnlyList<LocalMapPassage> Passages
);

internal class GetLocalMapQueryHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    IQueryHandler<GetRoomsByBuildingIdQuery, IReadOnlyCollection<Room>> getRoomsByBuildingId,
    IQueryHandler<
        GetConnectorsByOriginLocationIdsQuery,
        IReadOnlyCollection<LocationConnector>
    > getConnectorsByOriginLocationIds,
    IQueryHandler<GetLocalMapLocksQuery, IReadOnlyDictionary<Guid, LocalMapLockKind>> getLocks,
    IQueryHandler<GetLocalMapMarkersQuery, IReadOnlyList<LocalMapMarker>> getMarkers,
    IQueryHandler<GetVisitedRoomLocationIdsQuery, IReadOnlySet<Guid>> getVisitedRoomLocationIds
) : IQueryHandler<GetLocalMapQuery, LocalMapResult>
{
    public async Task<LocalMapResult> Handle(
        GetLocalMapQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = query.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), query.PlayerId);

        var building =
            await getBuildingByLocationId.Handle(
                new GetBuildingByLocationIdQuery { LocationId = player.LocationId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Building), player.LocationId);

        var rooms = await getRoomsByBuildingId.Handle(
            new GetRoomsByBuildingIdQuery { BuildingId = building.Id },
            cancellationToken
        );
        var currentRoom = rooms.Single(room => room.LocationId == player.LocationId);
        var roomLocationIds = rooms.Select(room => room.LocationId).ToArray();

        var visitedLocationIds = await getVisitedRoomLocationIds.Handle(
            new GetVisitedRoomLocationIdsQuery
            {
                CreatureId = player.Id,
                RoomLocationIds = roomLocationIds,
            },
            cancellationToken
        );
        var exploredLocationIds = visitedLocationIds.ToHashSet();
        exploredLocationIds.Add(currentRoom.LocationId);

        var markers = await getMarkers.Handle(
            new GetLocalMapMarkersQuery
            {
                PlayerId = player.Id,
                WorldId = player.WorldId,
                ExploredLocationIds = exploredLocationIds,
                RoomLocationIds = roomLocationIds,
            },
            cancellationToken
        );
        var corpseLocationIds = markers
            .Where(marker => marker.Kind == LocalMapMarkerKind.PlayerCorpse)
            .Select(marker => marker.LocationId)
            .ToHashSet();

        var connectors = await getConnectorsByOriginLocationIds.Handle(
            new GetConnectorsByOriginLocationIdsQuery { OriginLocationIds = roomLocationIds },
            cancellationToken
        );
        var roomsByLocationId = rooms.ToDictionary(room => room.LocationId);
        var interiorConnectors = connectors
            .Where(connector => roomsByLocationId.ContainsKey(connector.DestinationLocationId))
            .ToArray();
        var frontierLocationIds = interiorConnectors
            .Where(connector => exploredLocationIds.Contains(connector.OriginLocationId))
            .Select(connector => connector.DestinationLocationId)
            .Where(locationId => !exploredLocationIds.Contains(locationId))
            .ToHashSet();
        var visibleLocationIds = exploredLocationIds.Concat(frontierLocationIds).ToHashSet();
        visibleLocationIds.UnionWith(corpseLocationIds);

        var visibleRooms = rooms
            .Where(room => visibleLocationIds.Contains(room.LocationId))
            .Select(room => new LocalMapRoom(
                room.Id,
                room.LocationId,
                room.Name,
                room.FloorNumber,
                room.Bounds,
                room.Role,
                visitedLocationIds.Contains(room.LocationId)
                    || room.LocationId == currentRoom.LocationId,
                frontierLocationIds.Contains(room.LocationId),
                markers.Where(marker => marker.LocationId == room.LocationId).ToArray()
            ))
            .OrderBy(room => room.FloorNumber)
            .ThenBy(room => room.Name)
            .ToArray();

        var visibleConnectors = interiorConnectors
            .Where(connector =>
                visibleLocationIds.Contains(connector.OriginLocationId)
                && visibleLocationIds.Contains(connector.DestinationLocationId)
                && (
                    exploredLocationIds.Contains(connector.OriginLocationId)
                    || exploredLocationIds.Contains(connector.DestinationLocationId)
                )
            )
            .ToArray();
        var locks = await getLocks.Handle(
            new GetLocalMapLocksQuery
            {
                ConnectorIds = visibleConnectors.Select(connector => connector.Id).ToArray(),
            },
            cancellationToken
        );
        var passages = visibleConnectors
            .GroupBy(connector =>
            {
                var originRoomId = roomsByLocationId[connector.OriginLocationId].Id;
                var destinationRoomId = roomsByLocationId[connector.DestinationLocationId].Id;
                return originRoomId.CompareTo(destinationRoomId) <= 0
                    ? (originRoomId, destinationRoomId)
                    : (destinationRoomId, originRoomId);
            })
            .Select(group =>
            {
                var connector = group
                    .OrderByDescending(candidate => candidate.Path?.Points.Count >= 2)
                    .ThenBy(candidate => candidate.Id)
                    .First();
                return new LocalMapPassage(
                    connector.Id,
                    roomsByLocationId[connector.OriginLocationId].Id,
                    roomsByLocationId[connector.DestinationLocationId].Id,
                    connector.Path,
                    group
                        .Select(candidate => locks.GetValueOrDefault(candidate.Id))
                        .OrderByDescending(kind => kind)
                        .First()
                );
            })
            .ToArray();

        return new LocalMapResult(
            building.Id,
            building.Name,
            building.BuildingType,
            currentRoom.Id,
            visibleRooms,
            passages
        );
    }
}
