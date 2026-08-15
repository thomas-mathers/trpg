using Microsoft.EntityFrameworkCore;
using TRPG.Application.CreatureFormulas;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Commands;

public class RemoveInventoryItemCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid ItemId { get; init; }
    public required int Quantity { get; init; }
}

public class RemoveInventoryItemCommandHandler(TrpgDbContext context)
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

        var items = await context
            .Items.Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && i.Ownership.OwnerId == command.CreatureId
            )
            .ToListAsync(cancellationToken);

        var existing = items.FirstOrDefault(i => i.Id == command.ItemId);
        if (existing == null)
        {
            throw new InvalidOperationException(
                $"Item {command.ItemId} not found in creature {command.CreatureId}'s inventory."
            );
        }

        existing.Quantity -= command.Quantity;

        if (existing.Quantity <= 0)
        {
            context.Items.Remove(existing);
            items.Remove(existing);
        }

        var equippedItems = items.Where(i => i.Ownership.EquippedSlot != null).ToArray();
        StatFormulas.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);
    }
}
