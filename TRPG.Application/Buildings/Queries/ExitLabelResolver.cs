using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal record ResolvedExit(RoomConnector Connector, string DestinationLabel);

internal record DestinationLocationSummary(Guid Id, Guid? RoomId, Guid? DistrictId);

internal class ExitLabelResolver(TrpgDbContext context, GetRoomsByIdsQueryHandler getRoomsByIds)
{
    public async Task<IReadOnlyList<ResolvedExit>> Resolve(
        IReadOnlyCollection<RoomConnector> connectors,
        bool sourceIsIndoors,
        CancellationToken cancellationToken = default
    )
    {
        var destinationLocationIds = connectors
            .Select(c => c.DestinationLocationId)
            .Distinct()
            .ToArray();

        var destinationLocationsById = await context
            .Locations.AsNoTracking()
            .Where(l => destinationLocationIds.Contains(l.Id))
            .Select(l => new DestinationLocationSummary(l.Id, l.RoomId, l.DistrictId))
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        var destinationRoomIds = destinationLocationsById
            .Values.Where(l => l.RoomId != null)
            .Select(l => l.RoomId!.Value)
            .ToArray();
        var roomsById = await getRoomsByIds.Handle(
            new GetRoomsByIdsQuery { RoomIds = destinationRoomIds },
            cancellationToken
        );

        var destinationDistrictIds = destinationLocationsById
            .Values.Where(l => l.RoomId == null && l.DistrictId != null)
            .Select(l => l.DistrictId!.Value)
            .Distinct()
            .ToArray();
        var districtNamesById = await context
            .Districts.AsNoTracking()
            .Where(d => destinationDistrictIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);

        return connectors
            .Select(c => new ResolvedExit(
                c,
                ResolveLabel(
                    c,
                    sourceIsIndoors,
                    destinationLocationsById,
                    roomsById,
                    districtNamesById
                )
            ))
            .ToArray();
    }

    private static string ResolveLabel(
        RoomConnector connector,
        bool sourceIsIndoors,
        IReadOnlyDictionary<Guid, DestinationLocationSummary> destinationLocationsById,
        IReadOnlyDictionary<Guid, Room> roomsById,
        IReadOnlyDictionary<Guid, string> districtNamesById
    )
    {
        if (
            !destinationLocationsById.TryGetValue(
                connector.DestinationLocationId,
                out var destination
            )
        )
        {
            return "Outside";
        }

        if (destination.RoomId != null)
        {
            return roomsById.TryGetValue(destination.RoomId.Value, out var room)
                ? room.Name
                : "Outside";
        }

        if (sourceIsIndoors)
        {
            return "Outside";
        }

        return
            destination.DistrictId != null
            && districtNamesById.TryGetValue(destination.DistrictId.Value, out var name)
            ? name
            : "Outside";
    }
}
