using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetEquippedItemsByOwnersQuery
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class GetEquippedItemsByOwnersQueryHandler(IInventoryDbContext context)
    : IQueryHandler<GetEquippedItemsByOwnersQuery, IReadOnlyDictionary<Guid, IReadOnlyList<Item>>>
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Item>>> Handle(
        GetEquippedItemsByOwnersQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.CreatureIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Item>>();
        }

        var items = await context
            .Items.AsNoTracking()
            .Where(item =>
                item.Ownership.OwnerType == OwnerType.Creature
                && query.CreatureIds.AsEnumerable().Contains(item.Ownership.OwnerId)
                && item.Ownership.EquippedSlot != null
                && item.Quantity > 0
            )
            .ToArrayAsync(cancellationToken);

        return items
            .GroupBy(item => item.Ownership.OwnerId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Item>)group.ToArray());
    }
}
