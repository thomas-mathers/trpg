using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetConnectorsByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetConnectorsByLocationIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetConnectorsByLocationIdQuery, IReadOnlyCollection<LocationConnector>>
{
    public async Task<IReadOnlyCollection<LocationConnector>> Handle(
        GetConnectorsByLocationIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .LocationConnectors.AsNoTracking()
            .Where(c => c.OriginLocationId == query.LocationId)
            .ToArrayAsync(cancellationToken);
    }
}
