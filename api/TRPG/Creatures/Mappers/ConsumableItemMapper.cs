using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class ConsumableItemMapper
{
    public static ConsumableItemDetail ToDetail(this Consumable consumable, bool isQuestItem)
    {
        var equippedSlot = consumable.Ownership.EquippedSlot?.ToResponse();
        var rarity = consumable.Rarity.ToResponse();
        var modifiers = consumable.Modifiers.Select(modifier => modifier.ToSummary()).ToArray();
        var isStackable = Application.Inventory.ItemStackability.IsStackable(consumable);
        return new ConsumableItemDetail(
            consumable.Id,
            consumable.Name,
            consumable.Description,
            consumable.Weight,
            consumable.Quantity,
            equippedSlot,
            ItemType.Consumable,
            rarity,
            consumable.GoldValue,
            modifiers,
            isStackable,
            consumable.Resource.ToResponse(),
            consumable.RestoreAmount,
            consumable.Duration
        )
        {
            IsQuestItem = isQuestItem,
        };
    }
}
