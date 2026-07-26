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

        var currentlyEquipped = items.FirstOrDefault(i => i.Ownership.EquippedSlot == command.Slot);
        if (currentlyEquipped != null)
        {
            currentlyEquipped.Ownership.EquippedSlot = null;
            // Saved separately so the old occupant's slot clears before the new one is set —
            // doing both in one SaveChangesAsync can transiently violate the
            // ux_items_owner_equipped_slot unique index depending on statement order.
            await context.SaveChangesAsync(cancellationToken);
        }

        toEquip.Ownership.EquippedSlot = command.Slot;

        var equippedItems = items.Where(i => i.Ownership.EquippedSlot != null).ToArray();
        CreatureAttributesRecalculator.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);
    }
}
