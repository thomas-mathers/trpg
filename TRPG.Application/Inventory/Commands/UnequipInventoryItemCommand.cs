using Microsoft.EntityFrameworkCore;
using TRPG.Application.Creatures;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Commands;

internal class UnequipInventoryItemCommand
{
    public required Guid CreatureId { get; init; }
    public required EquipmentSlot Slot { get; init; }
}

internal class UnequipInventoryItemCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        UnequipInventoryItemCommand command,
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

        var item = inventoryItems.FirstOrDefault(i => i.EquippedSlot == command.Slot);
        if (item == null)
        {
            throw new InvalidOperationException(
                $"No item equipped in slot {command.Slot} for creature {command.CreatureId}."
            );
        }

        item.EquippedSlot = null;

        var equippedItems = inventoryItems
            .Where(i => i.EquippedSlot != null)
            .Select(i => i.Item)
            .ToArray();
        CreatureAttributesRecalculator.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);
    }
}
