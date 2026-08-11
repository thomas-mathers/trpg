using Microsoft.EntityFrameworkCore;
using TRPG.Application.Creatures;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory;

internal record GoldTransfer(Guid WorldId, ItemOwnerReference To, int Amount);

internal class InventoryItemTransfer(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<GoldTransfer>> Transfer(
        ItemOwnerReference from,
        ItemOwnerReference to,
        IReadOnlyList<ItemSelection> selections,
        CancellationToken cancellationToken = default
    )
    {
        var goldTransfers = new List<GoldTransfer>();
        var itemIds = selections.Select(item => item.ItemId).ToArray();

        var items = await context
            .Items.Where(item => itemIds.AsEnumerable().Contains(item.Id))
            .ToListAsync(cancellationToken);

        foreach (var selection in selections)
        {
            var item =
                items.FirstOrDefault(item => item.Id == selection.ItemId)
                ?? throw new InvalidOperationException(
                    $"Item {selection.ItemId} not found in {from.Type} {from.Id}'s inventory."
                );

            if (item.Ownership.OwnerId != from.Id || item.Ownership.OwnerType != from.Type)
            {
                throw new InvalidOperationException(
                    $"Item {selection.ItemId} is not owned by {from.Type} {from.Id}."
                );
            }

            if (selection.Quantity <= 0 || selection.Quantity > item.Quantity)
            {
                throw new InvalidOperationException(
                    $"Cannot transfer {selection.Quantity} of item {selection.ItemId}; only {item.Quantity} available."
                );
            }

            if (selection.Quantity < item.Quantity && !ItemStackability.IsStackable(item))
            {
                throw new InvalidOperationException(
                    $"Cannot partially transfer non-stackable item {selection.ItemId}."
                );
            }

            if (item is Gold goldItem)
            {
                goldItem.Quantity -= selection.Quantity;
                goldTransfers.Add(new GoldTransfer(goldItem.WorldId, to, selection.Quantity));
            }
            else if (selection.Quantity == item.Quantity)
            {
                item.Ownership.OwnerId = to.Id;
                item.Ownership.OwnerType = to.Type;
                item.Ownership.EquippedSlot = null;
                item.Ownership.AcquiredAt = DateTime.UtcNow;
            }
            else
            {
                item.Quantity -= selection.Quantity;
                context.Items.Add(
                    ItemEquipmentPolicy.Split(item, selection.Quantity, to.Id, to.Type)
                );
            }
        }

        if (from.Type == OwnerType.Creature)
        {
            await RecalculateCreatureAttributes(from.Id, cancellationToken);
        }

        return goldTransfers;
    }

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

        CreatureAttributesRecalculator.Recalculate(creature, equippedItems);
    }
}
