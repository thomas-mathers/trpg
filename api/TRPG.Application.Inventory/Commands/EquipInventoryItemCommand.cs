using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Application.CreatureFormulas;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Commands;

public class EquipInventoryItemCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid ItemId { get; init; }
    public required EquipmentSlot Slot { get; init; }
}

internal class EquipInventoryItemCommandHandler(TrpgDbContext context)
    : ICommandHandler<EquipInventoryItemCommand>
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

        await UnequipConflictingItems(toEquip, command.Slot, items, cancellationToken);

        toEquip.Ownership.EquippedSlot = ItemEquipmentPolicy.ResolveEquippedSlot(
            toEquip,
            command.Slot
        );

        var equippedItems = items.Where(i => i.Ownership.EquippedSlot != null).ToArray();
        StatFormulas.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task UnequipConflictingItems(
        Item toEquip,
        EquipmentSlot slot,
        IReadOnlyCollection<Item> items,
        CancellationToken cancellationToken
    )
    {
        var currentlyEquipped = items.Where(i => i.Ownership.EquippedSlot != null).ToArray();
        var conflicting = ItemEquipmentPolicy.GetConflictingItems(toEquip, slot, currentlyEquipped);

        if (conflicting.Count == 0)
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
