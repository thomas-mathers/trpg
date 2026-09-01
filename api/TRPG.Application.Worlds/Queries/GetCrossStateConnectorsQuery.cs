using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;

namespace TRPG.Application.Worlds.Queries;

public record CrossStateConnector(
    Guid Id,
    string Name,
    Guid OriginStateId,
    Guid DestinationStateId
);

public class GetCrossStateConnectorsQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetCrossStateConnectorsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetCrossStateConnectorsQuery, IReadOnlyList<CrossStateConnector>>
{
    public async Task<IReadOnlyList<CrossStateConnector>> Handle(
        GetCrossStateConnectorsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await (
            from connector in context.LocationConnectors.AsNoTracking()
            where connector.WorldId == query.WorldId
            join origin in context.Locations.AsNoTracking()
                on connector.OriginLocationId equals origin.Id
            join destination in context.Locations.AsNoTracking()
                on connector.DestinationLocationId equals destination.Id
            where origin.StateId != destination.StateId
            select new CrossStateConnector(
                connector.Id,
                connector.Name,
                origin.StateId,
                destination.StateId
            )
        ).ToArrayAsync(cancellationToken);
}
