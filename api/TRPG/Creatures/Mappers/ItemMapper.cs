using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class ItemMapper
{
    public static ItemDetail ToDetail(this Item item, bool isQuestItem = false) =>
        item switch
        {
            Weapon weapon => weapon.ToDetail(isQuestItem),
            Armor armor => armor.ToDetail(isQuestItem),
            Shield shield => shield.ToDetail(isQuestItem),
            Accessory accessory => accessory.ToDetail(isQuestItem),
            Ammunition ammunition => ammunition.ToDetail(isQuestItem),
            Consumable consumable => consumable.ToDetail(isQuestItem),
            Gold gold => gold.ToDetail(isQuestItem),
            Key key => key.ToDetail(isQuestItem),
            Book book => book.ToDetail(isQuestItem),
            _ => item.ToMiscDetail(isQuestItem),
        };

    public static ItemDetail[] ToDetails(
        this IEnumerable<Item> items,
        IReadOnlyCollection<Guid> questItemIds
    ) => items.Select(item => item.ToDetail(questItemIds.Contains(item.Id))).ToArray();

    // A plain Item with no specialized subtype — e.g. a courier package or other flavor-only
    // quest token that carries no stats of its own.
    private static MiscDetail ToMiscDetail(this Item item, bool isQuestItem)
    {
        var equippedSlot = item.Ownership.EquippedSlot?.ToResponse();
        var modifiers = item.Modifiers.Select(modifier => modifier.ToSummary()).ToArray();
        var isStackable = Application.Inventory.ItemStackability.IsStackable(item);
        return new MiscDetail(
            item.Id,
            item.Name,
            item.Description,
            item.Weight,
            item.Quantity,
            equippedSlot,
            ItemType.Misc,
            null,
            item.GoldValue,
            modifiers,
            isStackable
        )
        {
            IsQuestItem = isQuestItem,
        };
    }
}
