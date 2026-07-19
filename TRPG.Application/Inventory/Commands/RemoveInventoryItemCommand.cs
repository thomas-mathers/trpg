using Microsoft.EntityFrameworkCore;
using TRPG.Application.Creatures;
using TRPG.Data;

namespace TRPG.Application.Inventory.Commands;

internal class RemoveInventoryItemCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid ItemId { get; init; }
    public required int Quantity { get; init; }
}

internal class RemoveInventoryItemCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        RemoveInventoryItemCommand command,
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
            .ToListAsync(cancellationToken);

        var existing = inventoryItems.FirstOrDefault(i => i.ItemId == command.ItemId);
        if (existing == null)
        {
            throw new InvalidOperationException(
                $"Item {command.ItemId} not found in creature {command.CreatureId}'s inventory."
            );
        }

        existing.Quantity -= command.Quantity;

        if (existing.Quantity <= 0)
        {
            context.InventoryItems.Remove(existing);
            inventoryItems.Remove(existing);
        }

        var equippedItems = inventoryItems
            .Where(i => i.EquippedSlot != null)
            .Select(i => i.Item)
            .ToArray();
        CreatureAttributesRecalculator.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);
    }
}
