namespace TRPG.Contracts.Inventory.Responses;

public record InventorySummary(int Gold, IReadOnlyList<ItemSummary> Items);
