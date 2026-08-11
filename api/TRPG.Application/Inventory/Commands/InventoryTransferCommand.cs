using Microsoft.EntityFrameworkCore;
using TRPG.Application.Creatures;
using TRPG.Application.Inventory;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Commands;

internal class InventoryTransferCommand
{
    public required ItemOwnerReference From { get; init; }
    public required ItemOwnerReference To { get; init; }
    public required IReadOnlyList<ItemSelection> Items { get; init; }
}

internal class InventoryTransferCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        InventoryTransferCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Items.Count > 0)
        {
            await TransferItems(command, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task TransferItems(
        InventoryTransferCommand command,
        CancellationToken cancellationToken
    )
    {
        var itemIds = command.Items.Select(i => i.ItemId).ToArray();
        var items = await context
            .Items.Where(i => itemIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        foreach (var selection in command.Items)
        {
            var item =
                items.FirstOrDefault(i => i.Id == selection.ItemId)
                ?? throw new InvalidOperationException(
                    $"Item {selection.ItemId} not found in {command.From.Type} {command.From.Id}'s inventory."
                );

            if (
                item.Ownership.OwnerId != command.From.Id
                || item.Ownership.OwnerType != command.From.Type
            )
            {
                throw new InvalidOperationException(
                    $"Item {selection.ItemId} is not owned by {command.From.Type} {command.From.Id}."
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
                await TransferGold(goldItem, command.To, selection.Quantity, cancellationToken);
            }
            else if (selection.Quantity == item.Quantity)
            {
                item.Ownership.OwnerId = command.To.Id;
                item.Ownership.OwnerType = command.To.Type;
                item.Ownership.EquippedSlot = null;
                item.Ownership.AcquiredAt = DateTime.UtcNow;
            }
            else
            {
                item.Quantity -= selection.Quantity;
                context.Items.Add(
                    ItemEquipmentPolicy.Split(
                        item,
                        selection.Quantity,
                        command.To.Id,
                        command.To.Type
                    )
                );
            }
        }

        if (command.From.Type == OwnerType.Creature)
        {
            await RecalculateCreatureAttributes(command.From.Id, cancellationToken);
        }
    }

    private async Task RecalculateCreatureAttributes(
        Guid creatureId,
        CancellationToken cancellationToken
    )
    {
        var fromCreature =
            await context.Creatures.FirstOrDefaultAsync(c => c.Id == creatureId, cancellationToken)
            ?? throw new InvalidOperationException($"Creature {creatureId} not found.");

        var remainingEquipped = await context
            .Items.Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && i.Ownership.OwnerId == creatureId
                && i.Ownership.EquippedSlot != null
            )
            .ToListAsync(cancellationToken);
        CreatureAttributesRecalculator.Recalculate(fromCreature, remainingEquipped);
    }

    private async Task TransferGold(
        Gold sourceGoldItem,
        ItemOwnerReference to,
        int amount,
        CancellationToken cancellationToken
    )
    {
        sourceGoldItem.Quantity -= amount;
        var toGoldItem = await FindOrCreateGoldItem(to, sourceGoldItem.WorldId, cancellationToken);
        toGoldItem.Quantity += amount;
    }

    private async Task<Gold?> FindGoldItem(
        ItemOwnerReference owner,
        CancellationToken cancellationToken
    ) =>
        await context
            .Items.OfType<Gold>()
            .FirstOrDefaultAsync(
                i => i.Ownership.OwnerType == owner.Type && i.Ownership.OwnerId == owner.Id,
                cancellationToken
            );

    private async Task<Gold> FindOrCreateGoldItem(
        ItemOwnerReference owner,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var existing = await FindGoldItem(owner, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var goldItem = new Gold
        {
            WorldId = worldId,
            Name = "Gold",
            Ownership = new ItemOwnership { OwnerId = owner.Id, OwnerType = owner.Type },
        };
        context.Items.Add(goldItem);
        return goldItem;
    }
}
