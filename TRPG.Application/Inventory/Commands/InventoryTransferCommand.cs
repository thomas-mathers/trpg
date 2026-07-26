using Microsoft.EntityFrameworkCore;
using TRPG.Application.Creatures;
using TRPG.Application.Inventory;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Commands;

internal class InventoryTransferCommand
{
    public required Guid FromCreatureId { get; init; }
    public required Guid ToCreatureId { get; init; }
    public required IReadOnlyList<LootItemSelection> Items { get; init; }
}

internal class InventoryTransferCommandHandler(TrpgDbContext context, GoldService goldService)
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

        if (command.Items.Count > 0)
        {
            await TransferItems(command, fromCreature, toCreature, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task TransferItems(
        InventoryTransferCommand command,
        Creature fromCreature,
        Creature toCreature,
        CancellationToken cancellationToken
    )
    {
        var itemIds = command.Items.Select(i => i.ItemId).ToArray();
        var items = await context
            .Items.Where(i => itemIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        foreach (var selection in command.Items)
        {
            var item =
                items.FirstOrDefault(i => i.Id == selection.ItemId)
                ?? throw new InvalidOperationException(
                    $"Item {selection.ItemId} not found in creature {command.FromCreatureId}'s inventory."
                );

            if (item.Ownership.OwnerId != command.FromCreatureId)
            {
                throw new InvalidOperationException(
                    $"Item {selection.ItemId} is not owned by creature {command.FromCreatureId}."
                );
            }

            if (selection.Quantity <= 0 || selection.Quantity > item.Quantity)
            {
                throw new InvalidOperationException(
                    $"Cannot transfer {selection.Quantity} of item {selection.ItemId}; only {item.Quantity} available."
                );
            }

            if (item is Gold goldItem)
            {
                await goldService.Transfer(
                    goldItem,
                    fromCreature,
                    toCreature,
                    selection.Quantity,
                    cancellationToken
                );
            }
            else if (selection.Quantity == item.Quantity)
            {
                item.Ownership.OwnerId = command.ToCreatureId;
                item.Ownership.EquippedSlot = null;
                item.Ownership.AcquiredAt = DateTime.UtcNow;
            }
            else
            {
                item.Quantity -= selection.Quantity;

                var split = ItemEquipmentPolicy.Clone(item);
                split.Quantity = selection.Quantity;
                split.Ownership.OwnerId = command.ToCreatureId;
                split.Ownership.OwnerType = OwnerType.Creature;
                context.Items.Add(split);
            }
        }

        var remainingEquipped = await context
            .Items.Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && i.Ownership.OwnerId == command.FromCreatureId
                && i.Ownership.EquippedSlot != null
            )
            .ToListAsync(cancellationToken);
        CreatureAttributesRecalculator.Recalculate(fromCreature, remainingEquipped);
    }
}
