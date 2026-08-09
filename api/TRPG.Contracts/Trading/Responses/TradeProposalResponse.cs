using TRPG.Contracts.Inventory.Responses;

namespace TRPG.Contracts.Trading.Responses;

public enum TradeProposalStatus
{
    Accepted,
    Rejected,
}

public record TradeSnapshot(InventorySummary PlayerInventory, InventorySummary ShopInventory);

public record TradeProposalResponse(TradeProposalStatus Status);
