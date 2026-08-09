using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Queries;

internal class GetInventorySummaryByOwnerQuery
{
    public required ItemOwnerReference Owner { get; init; }
}

internal record InventorySnapshot(int Gold, IReadOnlyList<Item> Items);

internal class GetInventorySummaryByOwnerQueryHandler(
    GetInventoryByOwnerQueryHandler getInventoryByOwner
)
{
    public async Task<InventorySnapshot> Handle(
        GetInventorySummaryByOwnerQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var items = await getInventoryByOwner.Handle(
            new GetInventoryByOwnerQuery { Owner = query.Owner },
            cancellationToken
        );

        var gold = items.OfType<Gold>().Sum(i => i.Quantity);

        return new InventorySnapshot(gold, items);
    }
}
