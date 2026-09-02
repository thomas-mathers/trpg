using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Inventory.Queries;

public class GetEquippedItemCountQuery
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> ItemIds { get; init; }
}

internal class GetEquippedItemCountQueryHandler(IInventoryDbContext context)
    : IQueryHandler<GetEquippedItemCountQuery, int>
{
    public async Task<int> Handle(
        GetEquippedItemCountQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Items.AsNoTracking()
            .Where(item =>
                item.WorldId == query.WorldId
                && query.ItemIds.AsEnumerable().Contains(item.Id)
                && item.Ownership.EquippedSlot != null
            )
            .CountAsync(cancellationToken);
}
