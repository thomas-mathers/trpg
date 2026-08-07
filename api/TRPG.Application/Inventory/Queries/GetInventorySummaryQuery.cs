using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Queries;

internal class GetInventorySummaryQuery
{
    public required Guid CreatureId { get; init; }
}

internal record InventorySnapshot(int Gold, IReadOnlyList<Item> Items);

internal class GetInventorySummaryQueryHandler(
    GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId
)
{
    public async Task<InventorySnapshot> Handle(
        GetInventorySummaryQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var items = await getInventoryByCreatureId.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = query.CreatureId },
            cancellationToken
        );

        var gold = items.OfType<Gold>().Sum(i => i.Quantity);

        return new InventorySnapshot(gold, items);
    }
}
