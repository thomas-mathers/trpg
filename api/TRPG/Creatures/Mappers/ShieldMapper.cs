using TRPG.Contracts.Inventory.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class ShieldMapper
{
    public static ShieldDetail ToDetail(this Shield shield, bool isQuestItem)
    {
        var equippedSlot = shield.Ownership.EquippedSlot?.ToContract();
        var rarity = shield.Rarity.ToContract();
        var modifiers = shield.Modifiers.Select(modifier => modifier.ToSummary()).ToArray();
        var isStackable = Application.Inventory.ItemStackability.IsStackable(shield);
        return new ShieldDetail(
            shield.Id,
            shield.Name,
            shield.Description,
            shield.Weight,
            shield.Quantity,
            equippedSlot,
            ItemType.Shield,
            rarity,
            shield.GoldValue,
            modifiers,
            isStackable,
            shield.BlockChance,
            shield.Defense,
            shield.DurabilityCurrent,
            shield.DurabilityMax
        )
        {
            IsQuestItem = isQuestItem,
        };
    }
}
