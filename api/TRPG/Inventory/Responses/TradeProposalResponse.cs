namespace TRPG.Inventory.Responses;

public enum TradeProposalStatus
{
    Accepted,
    Rejected,
    Refused,
}

public record TradeSnapshot(InventorySummary PlayerInventory, InventorySummary ShopInventory);

public record TradeProposalResponse(TradeProposalStatus Status);
