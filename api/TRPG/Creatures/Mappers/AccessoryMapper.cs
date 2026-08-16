using TRPG.Contracts.Inventory.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class AccessoryMapper
{
    public static AccessoryDetail ToDetail(this Accessory accessory, bool isQuestItem)
    {
        var type = accessory.Type.ToContract();
        var equippedSlot = accessory.Ownership.EquippedSlot?.ToContract();
        var rarity = accessory.Rarity.ToContract();
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
