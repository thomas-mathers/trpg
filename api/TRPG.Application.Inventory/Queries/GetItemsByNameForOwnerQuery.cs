using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetItemsByNameForOwnerQuery
{
    public required Guid WorldId { get; init; }
    public required Guid OwnerId { get; init; }
    public required OwnerType OwnerType { get; init; }
    public required string Name { get; init; }
}

internal class GetItemsByNameForOwnerQueryHandler(IInventoryDbContext context)
    : IQueryHandler<GetItemsByNameForOwnerQuery, IReadOnlyList<Item>>
{
    public async Task<IReadOnlyList<Item>> Handle(
        GetItemsByNameForOwnerQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Items.AsNoTracking()
            .Where(item =>
                item.WorldId == query.WorldId
                && item.Ownership.OwnerId == query.OwnerId
                && item.Ownership.OwnerType == query.OwnerType
                && item.Name == query.Name
            )
            .ToListAsync(cancellationToken);
}
