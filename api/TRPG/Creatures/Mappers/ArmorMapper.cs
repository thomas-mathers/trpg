using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class ArmorMapper
{
    public static ArmorDetail ToDetail(this Armor armor, bool isQuestItem)
    {
        var equippedSlot = armor.Ownership.EquippedSlot?.ToResponse();
        var type = armor.Type.ToResponse();
        var rarity = armor.Rarity.ToResponse();
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
            armor.ArmorClass.ToResponse(),
            armor.DurabilityCurrent,
            armor.DurabilityMax
        )
        {
            IsQuestItem = isQuestItem,
        };
    }
}
