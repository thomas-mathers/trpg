using TRPG.Domain.Models;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Mappers;

internal static class BookMapper
{
    public static BookDetail ToDetail(this Book book, bool isQuestItem)
    {
        var equippedSlot = book.Ownership.EquippedSlot?.ToResponse();
        var modifiers = book.Modifiers.Select(modifier => modifier.ToSummary()).ToArray();
        var isStackable = Application.Inventory.ItemStackability.IsStackable(book);
        return new BookDetail(
            book.Id,
            book.Name,
            book.Description,
            book.Weight,
            book.Quantity,
            equippedSlot,
            ItemType.Book,
            null,
            book.GoldValue,
            modifiers,
            isStackable
        )
        {
            IsQuestItem = isQuestItem,
        };
    }
}
