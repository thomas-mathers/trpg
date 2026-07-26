using Microsoft.EntityFrameworkCore;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data;

namespace TRPG.Application.Inventory.Commands;

internal class InventoryTransferCommand
{
    public required Guid FromCreatureId { get; init; }
    public required Guid ToCreatureId { get; init; }
    public required bool TakeGold { get; init; }
    public required IReadOnlyList<LootItemSelection> Items { get; init; }
}

internal class InventoryTransferCommandHandler(
    TrpgDbContext context,
    RemoveInventoryItemCommandHandler removeInventoryItem,
    AddInventoryItemCommandHandler addInventoryItem
)
{
    public async Task Handle(
        InventoryTransferCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var fromCreature =
            await context.Creatures.FirstOrDefaultAsync(
                c => c.Id == command.FromCreatureId,
                cancellationToken
            )
            ?? throw new InvalidOperationException($"Creature {command.FromCreatureId} not found.");
        var toCreature =
            await context.Creatures.FirstOrDefaultAsync(
                c => c.Id == command.ToCreatureId,
                cancellationToken
            ) ?? throw new InvalidOperationException($"Creature {command.ToCreatureId} not found.");

        var sameLocation =
            fromCreature.RoomId != null
                ? fromCreature.RoomId == toCreature.RoomId
                : fromCreature.DistrictId == toCreature.DistrictId;
        if (!sameLocation)
        {
            throw new InvalidOperationException(
                $"Creature {command.FromCreatureId} is not nearby."
            );
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );

        if (command.TakeGold && fromCreature.Gold > 0)
        {
            var gold = fromCreature.Gold;
            fromCreature.Gold = 0;
            toCreature.Gold += gold;
            await context.SaveChangesAsync(cancellationToken);
        }

        foreach (var selection in command.Items)
        {
            await removeInventoryItem.Handle(
                new RemoveInventoryItemCommand
                {
                    CreatureId = command.FromCreatureId,
                    ItemId = selection.ItemId,
                    Quantity = selection.Quantity,
                },
                cancellationToken
            );
            await addInventoryItem.Handle(
                new AddInventoryItemCommand
                {
                    CreatureId = command.ToCreatureId,
                    ItemId = selection.ItemId,
                    Quantity = selection.Quantity,
                },
                cancellationToken
            );
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
