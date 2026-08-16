using TRPG.Contracts.Inventory.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class WeaponMapper
{
    public static WeaponDetail ToDetail(this Weapon weapon, bool isQuestItem)
    {
        var equippedSlot = weapon.Ownership.EquippedSlot?.ToContract();
        var type = weapon.Type.ToContract();
        var rarity = weapon.Rarity.ToContract();
        var modifiers = weapon.Modifiers.Select(modifier => modifier.ToSummary()).ToArray();
        var isStackable = Application.Inventory.ItemStackability.IsStackable(weapon);
        return new WeaponDetail(
            weapon.Id,
            weapon.Name,
            weapon.Description,
            weapon.Weight,
            weapon.Quantity,
            equippedSlot,
            type,
            rarity,
            weapon.GoldValue,
            modifiers,
            isStackable,
            weapon.MinDamage,
            weapon.MaxDamage,
            weapon.Range,
            weapon.AttacksPerTurn,
            weapon.IsTwoHanded,
            weapon.DurabilityCurrent,
            weapon.DurabilityMax
        )
        {
            IsQuestItem = isQuestItem,
        };
    }
}
