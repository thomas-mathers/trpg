using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetInventoryItemsByOwnerQuery
{
    public required ItemOwnerReference Owner { get; init; }
}

internal class GetInventoryItemsByOwnerQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetInventoryItemsByOwnerQuery, IReadOnlyList<Item>>
{
    public async Task<IReadOnlyList<Item>> Handle(
        GetInventoryItemsByOwnerQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Items.AsNoTracking()
            .Where(i =>
                i.Ownership.OwnerType == query.Owner.Type
                && i.Ownership.OwnerId == query.Owner.Id
                && i.Quantity > 0
            )
            .OrderBy(i => i.Ownership.AcquiredAt)
            .ToArrayAsync(cancellationToken);
    }
}
