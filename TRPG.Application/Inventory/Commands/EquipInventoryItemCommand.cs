using Microsoft.EntityFrameworkCore;
using TRPG.Application.Creatures;
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

        var inventoryItems = await context
            .InventoryItems.Include(i => i.Item)
            .Where(i => i.CreatureId == command.CreatureId)
            .ToArrayAsync(cancellationToken);

        var toEquip = inventoryItems.FirstOrDefault(i => i.ItemId == command.ItemId);
        if (toEquip == null)
        {
            throw new InvalidOperationException(
                $"Item {command.ItemId} not found in creature {command.CreatureId}'s inventory."
            );
        }

        var currentlyEquipped = inventoryItems.FirstOrDefault(i => i.EquippedSlot == command.Slot);
        if (currentlyEquipped != null)
        {
            currentlyEquipped.EquippedSlot = null;
        }

        toEquip.EquippedSlot = command.Slot;

        var equippedItems = inventoryItems
            .Where(i => i.EquippedSlot != null)
            .Select(i => i.Item)
            .ToArray();
        CreatureAttributesRecalculator.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);
    }
}
