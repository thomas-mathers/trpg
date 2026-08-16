using TRPG.Domain.Models;

namespace TRPG.Inventory.Requests;

public record InventoryTransferRequest(Guid FromId, Guid ToId, IReadOnlyList<ItemSelection> Items);
