using TRPG.Inventory.Responses;

namespace TRPG.Inventory.Requests;

public record EquipItemRequest(Guid ItemId, EquipmentSlot Slot);
