using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Worlds.Queries;

public class GetExitByDestinationNameQuery
{
    public required Guid LocationId { get; init; }
    public required string DestinationName { get; init; }
}

public record ExitMatch(bool Matched, Guid? DestinationLocationId);

public class GetExitByDestinationNameQueryHandler(TrpgDbContext context)
{
    public async Task<ExitMatch> Handle(
        GetExitByDestinationNameQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var match = await context
            .LocationConnectors.AsNoTracking()
            .Where(c => c.OriginLocationId == query.LocationId)
            .FirstOrDefaultAsync(
                c => EF.Functions.ILike(c.DestinationLabel, query.DestinationName),
                cancellationToken
            );

        return match != null
            ? new ExitMatch(true, match.DestinationLocationId)
            : new ExitMatch(false, null);
    }
}
