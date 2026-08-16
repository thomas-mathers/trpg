using TRPG.Contracts.Inventory.Responses;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Mappers;

internal static class ArmorMapper
{
    public static ArmorDetail ToDetail(this Armor armor, bool isQuestItem)
    {
        var equippedSlot = armor.Ownership.EquippedSlot?.ToContract();
        var type = armor.Type.ToContract();
        var rarity = armor.Rarity.ToContract();
        var modifiers = armor.Modifiers.Select(modifier => modifier.ToSummary()).ToArray();
        var isStackable = Application.Inventory.ItemStackability.IsStackable(armor);
        return new ArmorDetail(
            armor.Id,
            armor.Name,
            armor.Description,
            armor.Weight,
            armor.Quantity,
            equippedSlot,
            type,
            rarity,
            armor.GoldValue,
            modifiers,
            isStackable,
            armor.Defense,
            armor.ArmorClass.ToContract(),
            armor.DurabilityCurrent,
            armor.DurabilityMax
        )
        {
            IsQuestItem = isQuestItem,
        };
    }
}
