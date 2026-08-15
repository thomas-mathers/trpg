using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetInventoryByOwnerQuery
{
    public required ItemOwnerReference Owner { get; init; }
}

internal class GetInventoryByOwnerQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetInventoryByOwnerQuery, IReadOnlyList<Item>>
{
    public async Task<IReadOnlyList<Item>> Handle(
        GetInventoryByOwnerQuery query,
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
