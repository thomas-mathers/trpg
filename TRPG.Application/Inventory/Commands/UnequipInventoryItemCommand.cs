using Microsoft.EntityFrameworkCore;
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
        var item = await context.InventoryItems.FirstOrDefaultAsync(
            i => i.CreatureId == command.CreatureId && i.EquippedSlot == command.Slot,
            cancellationToken
        );

        if (item == null)
        {
            throw new InvalidOperationException(
                $"No item equipped in slot {command.Slot} for creature {command.CreatureId}."
            );
        }

        item.EquippedSlot = null;

        await context.SaveChangesAsync(cancellationToken);
    }
}
