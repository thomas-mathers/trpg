using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Trading;

internal class GetTradeQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid WorkstationId { get; init; }
}

internal record TradeSnapshotInfo(
    InventorySnapshot PlayerInventory,
    InventorySnapshot ShopInventory
);

internal class GetTradeQueryHandler(GetInventorySummaryByOwnerQueryHandler getInventorySummary)
{
    public async Task<TradeSnapshotInfo> Handle(
        GetTradeQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var playerInventory = await getInventorySummary.Handle(
            new GetInventorySummaryByOwnerQuery
            {
                Owner = new ItemOwnerReference(query.PlayerId, OwnerType.Creature),
            },
            cancellationToken
        );
        var shopInventory = await getInventorySummary.Handle(
            new GetInventorySummaryByOwnerQuery
            {
                Owner = new ItemOwnerReference(query.WorkstationId, OwnerType.Workstation),
            },
            cancellationToken
        );
        return new TradeSnapshotInfo(playerInventory, shopInventory);
    }
}
