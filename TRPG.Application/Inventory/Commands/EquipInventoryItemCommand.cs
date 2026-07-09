using Microsoft.EntityFrameworkCore;
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
        var toEquip = await context.InventoryItems.FirstOrDefaultAsync(
            i => i.CreatureId == command.CreatureId && i.ItemId == command.ItemId,
            cancellationToken
        );

        if (toEquip == null)
        {
            throw new InvalidOperationException(
                $"Item {command.ItemId} not found in creature {command.CreatureId}'s inventory."
            );
        }

        var currentlyEquipped = await context.InventoryItems.FirstOrDefaultAsync(
            i => i.CreatureId == command.CreatureId && i.EquippedSlot == command.Slot,
            cancellationToken
        );

        if (currentlyEquipped != null)
        {
            currentlyEquipped.EquippedSlot = null;
        }

        toEquip.EquippedSlot = command.Slot;

        await context.SaveChangesAsync(cancellationToken);
    }
}
