using TRPG.Contracts.Inventory.Responses;

namespace TRPG.Contracts.Inventory.Requests;

public record EquipItemRequest(Guid ItemId, EquipmentSlot Slot);
