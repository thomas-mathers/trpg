using TRPG.Contracts.Inventory.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class KeyMapper
{
    public static KeyDetail ToDetail(this Key key, bool isQuestItem)
    {
        var equippedSlot = key.Ownership.EquippedSlot?.ToContract();
        var modifiers = key.Modifiers.Select(modifier => modifier.ToSummary()).ToArray();
        var isStackable = Application.Inventory.ItemStackability.IsStackable(key);
        return new KeyDetail(
            key.Id,
            key.Name,
            key.Description,
            key.Weight,
            key.Quantity,
            equippedSlot,
            ItemType.Key,
            null,
            key.GoldValue,
            modifiers,
            isStackable
        )
        {
            IsQuestItem = isQuestItem,
        };
    }
}
