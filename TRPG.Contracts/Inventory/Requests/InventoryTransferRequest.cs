namespace TRPG.Contracts.Inventory.Requests;

public record LootItemSelection(Guid ItemId, int Quantity);

public record InventoryTransferRequest(bool TakeGold, IReadOnlyList<LootItemSelection> Items);
