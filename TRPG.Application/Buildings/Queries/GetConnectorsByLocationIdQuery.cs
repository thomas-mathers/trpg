using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetConnectorsByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetConnectorsByLocationIdQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<LocationConnector>> Handle(
        GetConnectorsByLocationIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .Where(p => p.LocationId == query.LocationId)
            .OfType<LocationConnector>()
            .ToArrayAsync(cancellationToken);
    }
}
