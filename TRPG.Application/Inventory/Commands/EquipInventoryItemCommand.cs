using Microsoft.EntityFrameworkCore;
using TRPG.Application.Creatures;
using TRPG.Application.Inventory;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Commands;

internal class EquipInventoryItemCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid ItemId { get; init; }
    public required EquipmentSlot Slot { get; init; }
}

internal class EquipInventoryItemCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        EquipInventoryItemCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await context.Creatures.FirstOrDefaultAsync(
            c => c.Id == command.CreatureId,
            cancellationToken
        );
        if (creature == null)
        {
            throw new InvalidOperationException($"Creature {command.CreatureId} not found.");
        }

        var items = await context
            .Items.Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && i.Ownership.OwnerId == command.CreatureId
            )
            .ToArrayAsync(cancellationToken);

        var toEquip = items.FirstOrDefault(i => i.Id == command.ItemId);
        if (toEquip == null)
        {
            throw new InvalidOperationException(
                $"Item {command.ItemId} not found in creature {command.CreatureId}'s inventory."
            );
        }

        if (ItemEquipmentPolicy.GetDefaultSlot(toEquip) == null)
        {
            throw new InvalidOperationException($"Item {command.ItemId} cannot be equipped.");
        }

        await UnequipConflictingItems(command, toEquip, items, cancellationToken);

        toEquip.Ownership.EquippedSlot = ItemEquipmentPolicy.ResolveEquippedSlot(
            toEquip,
            command.Slot
        );

        var equippedItems = items.Where(i => i.Ownership.EquippedSlot != null).ToArray();
        CreatureAttributesRecalculator.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task UnequipConflictingItems(
        EquipInventoryItemCommand command,
        Item toEquip,
        IReadOnlyCollection<Item> items,
        CancellationToken cancellationToken
    )
    {
        var newFootprint = ItemEquipmentPolicy.GetFootprint(toEquip, command.Slot);

        var conflicting = items
            .Where(i => i.Id != toEquip.Id && i.Ownership.EquippedSlot != null)
            .Where(i =>
                ItemEquipmentPolicy
                    .GetFootprint(i, i.Ownership.EquippedSlot!.Value)
                    .Intersect(newFootprint)
                    .Any()
            )
            .ToArray();

        if (conflicting.Length == 0)
        {
            return;
        }

        foreach (var item in conflicting)
        {
            item.Ownership.EquippedSlot = null;
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
