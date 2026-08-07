using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Buildings.Queries;

internal class GetKeyItemIdsQuery
{
    public required Guid LocationConnectorId { get; init; }
}

internal class GetKeyItemIdsQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetKeyItemIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .LocationConnectorKeys.Where(k => k.LocationConnectorId == query.LocationConnectorId)
            .Select(k => k.ItemId)
            .ToArrayAsync(cancellationToken);
    }
}
