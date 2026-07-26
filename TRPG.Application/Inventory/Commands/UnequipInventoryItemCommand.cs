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

        var items = await context
            .Items.Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && i.Ownership.OwnerId == command.CreatureId
            )
            .ToArrayAsync(cancellationToken);

        var item = items.FirstOrDefault(i => i.Ownership.EquippedSlot == command.Slot);
        if (item == null)
        {
            throw new InvalidOperationException(
                $"No item equipped in slot {command.Slot} for creature {command.CreatureId}."
            );
        }

        item.Ownership.EquippedSlot = null;

        var equippedItems = items.Where(i => i.Ownership.EquippedSlot != null).ToArray();
        CreatureAttributesRecalculator.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);
    }
}
