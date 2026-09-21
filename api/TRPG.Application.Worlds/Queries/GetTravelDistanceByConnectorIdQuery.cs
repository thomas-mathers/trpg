using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetTravelDistanceByConnectorIdQuery
{
    public required Guid ConnectorId { get; init; }
}

internal class GetTravelDistanceByConnectorIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetTravelDistanceByConnectorIdQuery, float?>
{
    public async Task<float?> Handle(
        GetTravelDistanceByConnectorIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .TravelConnectors.AsNoTracking()
            .Where(connector => connector.ConnectorId == query.ConnectorId)
            .Select(connector => (float?)connector.Distance)
            .FirstOrDefaultAsync(cancellationToken);
}
