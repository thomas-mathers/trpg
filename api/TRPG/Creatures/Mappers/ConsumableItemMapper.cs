using TRPG.Contracts.Inventory.Responses;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Mappers;

internal static class ConsumableItemMapper
{
    public static ConsumableItemDetail ToDetail(this Consumable consumable, bool isQuestItem)
    {
        var equippedSlot = consumable.Ownership.EquippedSlot?.ToContract();
        var rarity = consumable.Rarity.ToContract();
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
            consumable.Resource.ToContract(),
            consumable.RestoreAmount,
            consumable.Duration
        )
        {
            IsQuestItem = isQuestItem,
        };
    }
}
