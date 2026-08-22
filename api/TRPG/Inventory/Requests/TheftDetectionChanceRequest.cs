using TRPG.Domain.Models;

namespace TRPG.Inventory.Requests;

public record TheftDetectionChanceRequest(
    OwnerReferenceRequest From,
    IReadOnlyList<ItemSelection> Items
);
