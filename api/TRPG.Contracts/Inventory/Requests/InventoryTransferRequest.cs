namespace TRPG.Contracts.Inventory.Requests;

public record LootItemSelection(Guid ItemId, int Quantity);

public record InventoryTransferRequest(
    Guid FromId,
    Guid ToId,
    IReadOnlyList<LootItemSelection> Items
);
