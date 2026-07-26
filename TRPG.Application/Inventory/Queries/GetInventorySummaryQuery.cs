using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Queries;

internal class GetInventorySummaryQuery
{
    public required Guid CreatureId { get; init; }
    public required bool ConsumableOnly { get; init; }
}

internal record InventorySnapshot(int Gold, IReadOnlyList<InventoryItem> Items);

internal class GetInventorySummaryQueryHandler(
    TrpgDbContext context,
    GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId
)
{
    public async Task<InventorySnapshot> Handle(
        GetInventorySummaryQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var gold = await context
            .Creatures.AsNoTracking()
            .Where(c => c.Id == query.CreatureId)
            .Select(c => c.Gold)
            .FirstOrDefaultAsync(cancellationToken);

        var items = await getInventoryByCreatureId.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = query.CreatureId },
            cancellationToken
        );

        if (query.ConsumableOnly)
        {
            items = items.Where(i => i.Item is ConsumableItem).ToArray();
        }

        return new InventorySnapshot(gold, items);
    }
}
