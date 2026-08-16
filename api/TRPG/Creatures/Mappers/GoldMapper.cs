using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class GoldMapper
{
    public static GoldDetail ToDetail(this Gold gold, bool isQuestItem)
    {
        var equippedSlot = gold.Ownership.EquippedSlot?.ToResponse();
        var modifiers = gold.Modifiers.Select(modifier => modifier.ToSummary()).ToArray();
        var isStackable = Application.Inventory.ItemStackability.IsStackable(gold);
        return new GoldDetail(
            gold.Id,
            gold.Name,
            gold.Description,
            gold.Weight,
            gold.Quantity,
            equippedSlot,
            ItemType.Gold,
            null,
            gold.GoldValue,
            modifiers,
            isStackable
        )
        {
            IsQuestItem = isQuestItem,
        };
    }
}
