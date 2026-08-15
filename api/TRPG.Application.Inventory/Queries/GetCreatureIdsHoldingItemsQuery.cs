using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetCreatureIdsHoldingItemsQuery
{
    public required IReadOnlyCollection<Guid> ItemIds { get; init; }
}

public class GetCreatureIdsHoldingItemsQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetCreatureIdsHoldingItemsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Items.AsNoTracking()
            .Where(item =>
                query.ItemIds.AsEnumerable().Contains(item.Id)
                && item.Ownership.OwnerType == OwnerType.Creature
            )
            .Select(item => item.Ownership.OwnerId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
}
