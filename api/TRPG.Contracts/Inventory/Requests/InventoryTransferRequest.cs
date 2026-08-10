namespace TRPG.Contracts.Inventory.Requests;

public record InventoryTransferRequest(Guid FromId, Guid ToId, IReadOnlyList<ItemSelection> Items);
