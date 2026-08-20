using TRPG.Domain.Models;

namespace TRPG.Inventory.Requests;

public record OwnerReferenceRequest(Guid Id, OwnerType Type);

public record InventoryTransferRequest(
    OwnerReferenceRequest From,
    OwnerReferenceRequest To,
    IReadOnlyList<ItemSelection> Items
);
