using TRPG.Application.Common.Handling;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetInventorySummaryByOwnerQuery
{
    public required ItemOwnerReference Owner { get; init; }
}

public record InventorySnapshot(int Gold, IReadOnlyList<Item> Items);

internal class GetInventorySummaryByOwnerQueryHandler(
    IQueryHandler<GetInventoryByOwnerQuery, IReadOnlyList<Item>> getInventoryByOwner
) : IQueryHandler<GetInventorySummaryByOwnerQuery, InventorySnapshot>
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
