using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetConnectorsByOriginLocationIdsQuery
{
    public required IReadOnlyCollection<Guid> OriginLocationIds { get; init; }
}

internal class GetConnectorsByOriginLocationIdsQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetConnectorsByOriginLocationIdsQuery, IReadOnlyCollection<PlacedConnector>>
{
    public async Task<IReadOnlyCollection<PlacedConnector>> Handle(
        GetConnectorsByOriginLocationIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.OriginLocationIds.Count == 0)
        {
            return [];
        }

        var connectors = await context
            .LocationConnectors.AsNoTracking()
            .Where(connector =>
                query.OriginLocationIds.AsEnumerable().Contains(connector.OriginLocationId)
            )
            .ToArrayAsync(cancellationToken);
        var nodeIds = connectors
            .SelectMany(connector => new[] { connector.OriginNodeId, connector.DestinationNodeId })
            .Distinct()
            .ToArray();
        var nodes = await context
            .TravelNodes.AsNoTracking()
            .Where(node => nodeIds.Contains(node.Id))
            .ToArrayAsync(cancellationToken);

        return PlacedConnector.Place(connectors, nodes);
    }
}
