using TRPG.Contracts.Inventory.Responses;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Mappers;

internal static class AmmunitionMapper
{
    public static AmmunitionDetail ToDetail(this Ammunition ammunition, bool isQuestItem)
    {
        var type = ammunition.Type.ToContract();
        var equippedSlot = ammunition.Ownership.EquippedSlot?.ToContract();
        var rarity = ammunition.Rarity.ToContract();
        var modifiers = ammunition.Modifiers.Select(modifier => modifier.ToSummary()).ToArray();
        var isStackable = Application.Inventory.ItemStackability.IsStackable(ammunition);
        return new AmmunitionDetail(
            ammunition.Id,
            ammunition.Name,
            ammunition.Description,
            ammunition.Weight,
            ammunition.Quantity,
            equippedSlot,
            type,
            rarity,
            ammunition.GoldValue,
            modifiers,
            isStackable
        )
        {
            IsQuestItem = isQuestItem,
        };
    }
}
