using Microsoft.EntityFrameworkCore;
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
        var existing = await context.InventoryItems.FirstOrDefaultAsync(
            i => i.CreatureId == command.CreatureId && i.ItemId == command.ItemId,
            cancellationToken
        );

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
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
