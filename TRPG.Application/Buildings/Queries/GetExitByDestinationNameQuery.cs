using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetExitByDestinationNameQuery
{
    public required Guid RoomId { get; init; }
    public required string DestinationName { get; init; }
}

internal record ExitMatch(bool Matched, Guid? DestinationRoomId, Guid? DestinationLocationId);

internal class GetExitByDestinationNameQueryHandler(TrpgDbContext context)
{
    public async Task<ExitMatch> Handle(
        GetExitByDestinationNameQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var connectors = await context
            .Props.AsNoTracking()
            .Where(p => p.RoomId == query.RoomId)
            .OfType<RoomConnector>()
            .ToArrayAsync(cancellationToken);

        if (query.DestinationName.Equals("Outside", StringComparison.OrdinalIgnoreCase))
        {
            if (!connectors.Any(c => c.DestinationRoomId == null))
            {
                return new ExitMatch(false, null, null);
            }

            var roomLocationId = await context
                .Rooms.Where(r => r.Id == query.RoomId)
                .Select(r => r.LocationId)
                .FirstOrDefaultAsync(cancellationToken);
            var districtId = await context
                .Locations.Where(l => l.Id == roomLocationId)
                .Select(l => l.DistrictId)
                .FirstOrDefaultAsync(cancellationToken);
            var outdoorLocationId =
                districtId != null
                    ? await context
                        .Districts.Where(d => d.Id == districtId)
                        .Select(d => (Guid?)d.LocationId)
                        .FirstOrDefaultAsync(cancellationToken)
                    : null;

            return new ExitMatch(true, null, outdoorLocationId);
        }

        var destinationIds = connectors
            .Where(c => c.DestinationRoomId != null)
            .Select(c => c.DestinationRoomId!.Value)
            .ToHashSet();

        var destinationRoom = await context
            .Rooms.Where(r =>
                destinationIds.Contains(r.Id) && EF.Functions.ILike(r.Name, query.DestinationName)
            )
            .Select(r => new { r.Id, r.LocationId })
            .FirstOrDefaultAsync(cancellationToken);

        return destinationRoom != null
            ? new ExitMatch(true, destinationRoom.Id, destinationRoom.LocationId)
            : new ExitMatch(false, null, null);
    }
}
