using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetItemsByIdsForOwnerQuery
{
    public required Guid OwnerId { get; init; }
    public required OwnerType OwnerType { get; init; }
    public required IReadOnlyCollection<Guid> ItemIds { get; init; }
}

internal class GetItemsByIdsForOwnerQueryHandler(IInventoryDbContext context)
    : IQueryHandler<GetItemsByIdsForOwnerQuery, IReadOnlyList<Item>>
{
    public async Task<IReadOnlyList<Item>> Handle(
        GetItemsByIdsForOwnerQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Items.AsNoTracking()
            .Where(item =>
                query.ItemIds.AsEnumerable().Contains(item.Id)
                && item.Ownership.OwnerId == query.OwnerId
                && item.Ownership.OwnerType == query.OwnerType
            )
            .ToListAsync(cancellationToken);
}
