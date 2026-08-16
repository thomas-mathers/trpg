using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetInventoryItemsByOwnersQuery
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class GetInventoryItemsByOwnersQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetInventoryItemsByOwnersQuery, IReadOnlyDictionary<Guid, IReadOnlyList<Item>>>
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Item>>> Handle(
        GetInventoryItemsByOwnersQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var items = await context
            .Items.AsNoTracking()
            .Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && query.CreatureIds.AsEnumerable().Contains(i.Ownership.OwnerId)
                && i.Quantity > 0
            )
            .OrderBy(i => i.Ownership.AcquiredAt)
            .ToArrayAsync(cancellationToken);

        return items
            .GroupBy(item => item.Ownership.OwnerId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Item>)group.ToArray());
    }
}
