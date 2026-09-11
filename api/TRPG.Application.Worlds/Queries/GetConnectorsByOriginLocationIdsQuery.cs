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
    : IQueryHandler<GetConnectorsByOriginLocationIdsQuery, IReadOnlyCollection<LocationConnector>>
{
    public async Task<IReadOnlyCollection<LocationConnector>> Handle(
        GetConnectorsByOriginLocationIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.OriginLocationIds.Count == 0)
        {
            return [];
        }

        return await context
            .LocationConnectors.AsNoTracking()
            .Where(connector =>
                query.OriginLocationIds.AsEnumerable().Contains(connector.OriginLocationId)
            )
            .ToArrayAsync(cancellationToken);
    }
}
