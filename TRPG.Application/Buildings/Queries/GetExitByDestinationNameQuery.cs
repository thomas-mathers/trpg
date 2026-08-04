using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetExitByDestinationNameQuery
{
    public required Guid LocationId { get; init; }
    public required string DestinationName { get; init; }
}

internal record ExitMatch(bool Matched, Guid? DestinationLocationId);

internal class GetExitByDestinationNameQueryHandler(
    TrpgDbContext context,
    ExitLabelResolver exitLabelResolver
)
{
    public async Task<ExitMatch> Handle(
        GetExitByDestinationNameQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var sourceRoomId = await context
            .Locations.AsNoTracking()
            .Where(l => l.Id == query.LocationId)
            .Select(l => l.RoomId)
            .FirstOrDefaultAsync(cancellationToken);

        var connectors = await context
            .Props.AsNoTracking()
            .Where(p => p.LocationId == query.LocationId)
            .OfType<RoomConnector>()
            .ToArrayAsync(cancellationToken);

        var resolved = await exitLabelResolver.Resolve(
            connectors,
            sourceIsIndoors: sourceRoomId != null,
            cancellationToken
        );

        var match = resolved.FirstOrDefault(e =>
            string.Equals(
                e.DestinationLabel,
                query.DestinationName,
                StringComparison.OrdinalIgnoreCase
            )
        );

        return match != null
            ? new ExitMatch(true, match.Connector.DestinationLocationId)
            : new ExitMatch(false, null);
    }
}
