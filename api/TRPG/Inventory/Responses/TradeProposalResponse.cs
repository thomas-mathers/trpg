using TRPG.Inventory.Responses;

namespace TRPG.Inventory.Responses;

public enum TradeProposalStatus
{
    Accepted,
    Rejected,
}

public record TradeSnapshot(InventorySummary PlayerInventory, InventorySummary ShopInventory);

public record TradeProposalResponse(TradeProposalStatus Status);
