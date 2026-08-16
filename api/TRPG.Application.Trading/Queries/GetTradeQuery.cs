using TRPG.Application.Common.Handling;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Inventory.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Trading.Queries;

public class GetTradeQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid WorkstationId { get; init; }
}

public record TradeResult(InventoryResult PlayerInventory, InventoryResult ShopInventory);

internal class GetTradeQueryHandler(
    IQueryHandler<GetInventoryByOwnerQuery, InventoryResult> getInventory
) : IQueryHandler<GetTradeQuery, TradeResult>
{
    public async Task<TradeResult> Handle(
        GetTradeQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var playerInventory = await getInventory.Handle(
            new GetInventoryByOwnerQuery
            {
                Owner = new ItemOwnerReference(query.PlayerId, OwnerType.Creature),
            },
            cancellationToken
        );
        var shopInventory = await getInventory.Handle(
            new GetInventoryByOwnerQuery
            {
                Owner = new ItemOwnerReference(query.WorkstationId, OwnerType.Workstation),
            },
            cancellationToken
        );
        return new TradeResult(playerInventory, shopInventory);
    }
}
