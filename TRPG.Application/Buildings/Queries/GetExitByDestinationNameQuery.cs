using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetExitByDestinationNameQuery
{
    public required Guid RoomId { get; init; }
    public required string DestinationName { get; init; }
}

internal record ExitMatch(bool Matched, Guid? DestinationRoomId);

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
            return connectors.Any(c => c.DestinationRoomId == null)
                ? new ExitMatch(true, null)
                : new ExitMatch(false, null);
        }

        var destinationIds = connectors
            .Where(c => c.DestinationRoomId != null)
            .Select(c => c.DestinationRoomId!.Value)
            .ToHashSet();

        var destinationRoomId = await context
            .Rooms.Where(r =>
                destinationIds.Contains(r.Id) && EF.Functions.ILike(r.Name, query.DestinationName)
            )
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return destinationRoomId != null
            ? new ExitMatch(true, destinationRoomId)
            : new ExitMatch(false, null);
    }
}
