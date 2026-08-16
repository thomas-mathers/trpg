using TRPG.Contracts.Inventory.Responses;
using TRPG.Domain.Models;

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
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };

    public static ItemDetail[] ToDetails(
        this IEnumerable<Item> items,
        IReadOnlyCollection<Guid> questItemIds
    ) => items.Select(item => item.ToDetail(questItemIds.Contains(item.Id))).ToArray();
}
