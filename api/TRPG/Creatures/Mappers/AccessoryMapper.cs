using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class AccessoryMapper
{
    public static AccessoryDetail ToDetail(this Accessory accessory, bool isQuestItem)
    {
        var type = accessory.Type.ToResponse();
        var equippedSlot = accessory.Ownership.EquippedSlot?.ToResponse();
        var rarity = accessory.Rarity.ToResponse();
        var modifiers = accessory.Modifiers.Select(modifier => modifier.ToSummary()).ToArray();
        var isStackable = Application.Inventory.ItemStackability.IsStackable(accessory);
        return new AccessoryDetail(
            accessory.Id,
            accessory.Name,
            accessory.Description,
            accessory.Weight,
            accessory.Quantity,
            equippedSlot,
            type,
            rarity,
            accessory.GoldValue,
            modifiers,
            isStackable
        )
        {
            IsQuestItem = isQuestItem,
        };
    }
}
