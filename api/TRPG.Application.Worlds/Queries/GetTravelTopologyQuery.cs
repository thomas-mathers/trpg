using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public record TravelTopologyEdge(
    Guid WorldId,
    Guid ConnectorId,
    Guid OriginLocationId,
    Guid DestinationLocationId,
    float Distance
);

public class GetTravelTopologyQuery
{
    public IReadOnlyCollection<Guid> WorldIds { get; init; } = [];
    public IReadOnlyCollection<Guid> ConnectorIds { get; init; } = [];
}

internal class GetTravelTopologyQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetTravelTopologyQuery, IReadOnlyList<TravelTopologyEdge>>
{
    public async Task<IReadOnlyList<TravelTopologyEdge>> Handle(
        GetTravelTopologyQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.WorldIds.Count == 0 && query.ConnectorIds.Count == 0)
        {
            return [];
        }

        var connectors = context.LocationConnectors.AsNoTracking();
        if (query.WorldIds.Count > 0)
        {
            connectors = connectors.Where(connector =>
                query.WorldIds.AsEnumerable().Contains(connector.WorldId)
            );
        }
        if (query.ConnectorIds.Count > 0)
        {
            connectors = connectors.Where(connector =>
                query.ConnectorIds.AsEnumerable().Contains(connector.Id)
            );
        }

        return await (
            from connector in connectors
            join travel in context.TravelConnectors.AsNoTracking()
                on connector.Id equals travel.ConnectorId
            select new TravelTopologyEdge(
                connector.WorldId,
                connector.Id,
                connector.OriginLocationId,
                connector.DestinationLocationId,
                travel.Distance
            )
        ).ToArrayAsync(cancellationToken);
    }
}
