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

internal class GetExitByDestinationNameQueryHandler(TrpgDbContext context)
{
    public async Task<ExitMatch> Handle(
        GetExitByDestinationNameQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var match = await context
            .Props.AsNoTracking()
            .Where(p => p.LocationId == query.LocationId)
            .OfType<LocationConnector>()
            .FirstOrDefaultAsync(
                c => EF.Functions.ILike(c.DestinationLabel, query.DestinationName),
                cancellationToken
            );

        return match != null
            ? new ExitMatch(true, match.DestinationLocationId)
            : new ExitMatch(false, null);
    }
}
