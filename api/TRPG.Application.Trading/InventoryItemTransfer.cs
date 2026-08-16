using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Trading;

public class InventoryItemTransfer(TrpgDbContext context, ICommandHandler<AddGoldCommand> addGold)
{
    public async Task Transfer(
        ItemOwnerReference from,
        ItemOwnerReference to,
        IReadOnlyList<ItemSelection> selections,
        CancellationToken cancellationToken = default
    )
    {
        var transferItems = await ValidateTransfer(from, selections, cancellationToken);
        await MoveItems(transferItems, to, cancellationToken);
        await RecalculateSourceAttributes(from, cancellationToken);
    }

    private async Task<IReadOnlyCollection<TransferItem>> ValidateTransfer(
        ItemOwnerReference from,
        IReadOnlyList<ItemSelection> selections,
        CancellationToken cancellationToken
    )
    {
        var quantitiesByItemId = selections
            .GroupBy(selection => selection.ItemId)
            .ToDictionary(group => group.Key, group => group.Sum(selection => selection.Quantity));

        var itemsById = await context
            .Items.Where(item => quantitiesByItemId.Keys.AsEnumerable().Contains(item.Id))
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

    private async Task MoveItems(
        IReadOnlyCollection<TransferItem> transferItems,
        ItemOwnerReference to,
        CancellationToken cancellationToken
    )
    {
        foreach (var (item, quantity) in transferItems)
        {
            if (item is Gold goldItem)
            {
                goldItem.Quantity -= quantity;
                await addGold.Handle(
                    new AddGoldCommand
                    {
                        Owner = to,
                        WorldId = goldItem.WorldId,
                        Amount = quantity,
                    },
                    cancellationToken
                );
            }
            else if (quantity == item.Quantity)
            {
                item.Ownership.OwnerId = to.Id;
                item.Ownership.OwnerType = to.Type;
                item.Ownership.EquippedSlot = null;
                item.Ownership.AcquiredAt = DateTime.UtcNow;
            }
            else
            {
                item.Quantity -= quantity;
                context.Items.Add(Split(item, quantity, to));
            }
        }
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

    private static Item Split(Item item, int quantity, ItemOwnerReference owner)
    {
        var type = item.GetType();
        var node = JsonSerializer.SerializeToNode(item, type)!.AsObject();
        node[nameof(Item.Id)] = Guid.NewGuid();
        node[nameof(Item.Quantity)] = quantity;
        node[nameof(Item.Ownership)] = JsonSerializer.SerializeToNode(
            new ItemOwnership { OwnerId = owner.Id, OwnerType = owner.Type }
        );
        return (Item)node.Deserialize(type)!;
    }

    private async Task RecalculateSourceAttributes(
        ItemOwnerReference from,
        CancellationToken cancellationToken
    )
    {
        if (from.Type == OwnerType.Creature)
        {
            await RecalculateCreatureAttributes(from.Id, cancellationToken);
        }
    }

    private sealed record TransferItem(Item Item, int Quantity);

    private async Task RecalculateCreatureAttributes(
        Guid creatureId,
        CancellationToken cancellationToken
    )
    {
        var creature =
            await context.Creatures.FirstOrDefaultAsync(
                creature => creature.Id == creatureId,
                cancellationToken
            ) ?? throw new InvalidOperationException($"Creature {creatureId} not found.");

        var equippedItems = await context
            .Items.Where(item =>
                item.Ownership.OwnerType == OwnerType.Creature
                && item.Ownership.OwnerId == creatureId
                && item.Ownership.EquippedSlot != null
            )
            .ToListAsync(cancellationToken);

        StatFormulas.Recalculate(creature, equippedItems);
    }
}
