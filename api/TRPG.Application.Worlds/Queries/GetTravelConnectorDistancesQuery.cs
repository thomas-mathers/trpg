using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetTravelConnectorDistancesQuery
{
    public required IReadOnlyCollection<Guid> ConnectorIds { get; init; }
}

internal class GetTravelConnectorDistancesQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetTravelConnectorDistancesQuery, IReadOnlyDictionary<Guid, float>>
{
    public async Task<IReadOnlyDictionary<Guid, float>> Handle(
        GetTravelConnectorDistancesQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .TravelConnectors.AsNoTracking()
            .Where(connector => query.ConnectorIds.AsEnumerable().Contains(connector.ConnectorId))
            .ToDictionaryAsync(
                connector => connector.ConnectorId,
                connector => connector.Distance,
                cancellationToken
            );
}
