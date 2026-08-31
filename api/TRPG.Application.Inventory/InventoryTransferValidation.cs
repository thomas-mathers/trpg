using Microsoft.EntityFrameworkCore;
using TRPG.Application.CreatureFormulas;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory;

public sealed record TransferItem(Item Item, int Quantity);

public static class InventoryTransferValidation
{
    public static async Task<IReadOnlyCollection<TransferItem>> GetValidatedTransferItems(
        ItemOwnerReference from,
        IReadOnlyList<ItemSelection> selections,
        IQueryable<Item> items,
        CancellationToken cancellationToken
    )
    {
        var quantitiesByItemId = selections
            .GroupBy(selection => selection.ItemId)
            .ToDictionary(group => group.Key, group => group.Sum(selection => selection.Quantity));

        var itemsById = await items
            .Where(item => quantitiesByItemId.Keys.AsEnumerable().Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var missingItemIds = quantitiesByItemId.Keys.Except(itemsById.Keys).ToArray();

        if (missingItemIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Item {missingItemIds[0]} not found in {from.Type} {from.Id}'s inventory."
            );
        }

        var transferItems = new List<TransferItem>();

        foreach (var (itemId, quantity) in quantitiesByItemId)
        {
            var item = itemsById[itemId];
            ValidateItem(item, itemId, quantity, from);
            transferItems.Add(new TransferItem(item, quantity));
        }

        return transferItems;
    }

    private static void ValidateItem(Item item, Guid itemId, int quantity, ItemOwnerReference from)
    {
        if (item.Ownership.OwnerId != from.Id || item.Ownership.OwnerType != from.Type)
        {
            throw new InvalidOperationException(
                $"Item {itemId} is not owned by {from.Type} {from.Id}."
            );
        }

        if (quantity <= 0 || quantity > item.Quantity)
        {
            throw new InvalidOperationException(
                $"Cannot transfer {quantity} of item {itemId}; only {item.Quantity} available."
            );
        }

        if (quantity < item.Quantity && !ItemStackability.IsStackable(item))
        {
            throw new InvalidOperationException(
                $"Cannot partially transfer non-stackable item {itemId}."
            );
        }
    }
}
