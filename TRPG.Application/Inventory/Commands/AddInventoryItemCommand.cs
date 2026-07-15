using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Commands;

internal class AddInventoryItemCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid ItemId { get; init; }
    public required int Quantity { get; init; }
}

internal class AddInventoryItemCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        AddInventoryItemCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var isStackable = await context
            .Items.Where(i => i.Id == command.ItemId)
            .Select(i => i.IsStackable)
            .FirstAsync(cancellationToken);

        if (isStackable)
        {
            var existing = await context.InventoryItems.FirstOrDefaultAsync(
                i => i.CreatureId == command.CreatureId && i.ItemId == command.ItemId,
                cancellationToken
            );

            if (existing != null)
            {
                existing.Quantity += command.Quantity;
                await context.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        var maxIndex =
            await context
                .InventoryItems.Where(i => i.CreatureId == command.CreatureId)
                .MaxAsync(i => (int?)i.Index, cancellationToken)
            ?? -1;

        var worldId = await context
            .Creatures.Where(p => p.Id == command.CreatureId)
            .Select(p => p.WorldId)
            .FirstAsync(cancellationToken);

        context.InventoryItems.Add(
            new InventoryItem
            {
                Id = Guid.NewGuid(),
                CreatureId = command.CreatureId,
                ItemId = command.ItemId,
                Quantity = command.Quantity,
                Index = maxIndex + 1,
                WorldId = worldId,
            }
        );

        await context.SaveChangesAsync(cancellationToken);
    }
}
