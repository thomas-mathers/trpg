using TRPG.Application.Common.Handling;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Trading.Queries;

public class GetTradeQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid WorkstationId { get; init; }
}

public record TradeSnapshotInfo(InventorySnapshot PlayerInventory, InventorySnapshot ShopInventory);

internal class GetTradeQueryHandler(
    IQueryHandler<GetInventorySummaryByOwnerQuery, InventorySnapshot> getInventorySummary
) : IQueryHandler<GetTradeQuery, TradeSnapshotInfo>
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
