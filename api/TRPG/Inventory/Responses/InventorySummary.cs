namespace TRPG.Inventory.Responses;

public record InventorySummary(
    int Gold,
    IReadOnlyList<ItemDetail> Items,
    int Weight,
    int? CarryingCapacity
);
