using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Trading;

public record InventoryItemTransferResult(Guid SourceItemId, Guid DestinationItemId, int Quantity);

public class InventoryItemTransfer(TrpgDbContext context, ICommandHandler<AddGoldCommand> addGold)
{
    public async Task Validate(
        ItemOwnerReference from,
        IReadOnlyList<ItemSelection> selections,
        CancellationToken cancellationToken = default
    ) =>
        _ = await GetValidatedTransferItems(
            from,
            selections,
            context.Items.AsNoTracking(),
            cancellationToken
        );

    public async Task<IReadOnlyCollection<InventoryItemTransferResult>> Transfer(
        ItemOwnerReference from,
        ItemOwnerReference to,
        IReadOnlyList<ItemSelection> selections,
        CancellationToken cancellationToken = default
    )
    {
        var transferItems = await GetValidatedTransferItems(
            from,
            selections,
            context.Items,
            cancellationToken
        );
        var results = await MoveItems(transferItems, to, cancellationToken);
        await RecalculateSourceAttributes(from, cancellationToken);
        return results;
    }

    private async Task<IReadOnlyCollection<TransferItem>> GetValidatedTransferItems(
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

    private async Task<IReadOnlyCollection<InventoryItemTransferResult>> MoveItems(
        IReadOnlyCollection<TransferItem> transferItems,
        ItemOwnerReference to,
        CancellationToken cancellationToken
    )
    {
        var results = new List<InventoryItemTransferResult>();

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
                var destinationGold = await context
                    .Items.OfType<Gold>()
                    .FirstAsync(
                        candidate =>
                            candidate.Ownership.OwnerId == to.Id
                            && candidate.Ownership.OwnerType == to.Type,
                        cancellationToken
                    );
                results.Add(new(item.Id, destinationGold.Id, quantity));
            }
            else if (quantity == item.Quantity)
            {
                item.Ownership.OwnerId = to.Id;
                item.Ownership.OwnerType = to.Type;
                item.Ownership.EquippedSlot = null;
                item.Ownership.AcquiredAt = DateTime.UtcNow;
                results.Add(new(item.Id, item.Id, quantity));
            }
            else
            {
                item.Quantity -= quantity;
                var splitItem = Split(item, quantity, to);
                context.Items.Add(splitItem);
                results.Add(new(item.Id, splitItem.Id, quantity));
            }
        }

        return results;
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
