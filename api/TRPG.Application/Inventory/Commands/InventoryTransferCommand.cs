using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures;
using TRPG.Application.Quests;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Commands;

internal class InventoryTransferCommand
{
    public required ItemOwnerReference From { get; init; }
    public required ItemOwnerReference To { get; init; }
    public required IReadOnlyList<ItemSelection> Items { get; init; }
}

internal class InventoryTransferCommandHandler(TrpgDbContext context, QuestEventHandler questEvents)
{
    public async Task Handle(
        InventoryTransferCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        if (command.Items.Count > 0)
        {
            await TransferItems(command, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        var playerWorldId = await GetPlayerWorldId(command.To, cancellationToken);

        if (playerWorldId is not null)
        {
            foreach (var item in command.Items)
            {
                await questEvents.Handle(
                    new ItemAcquiredEvent(command.To.Id, playerWorldId.Value, item.ItemId),
                    cancellationToken
                );
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private Task<Guid?> GetPlayerWorldId(
        ItemOwnerReference owner,
        CancellationToken cancellationToken
    ) =>
        owner.Type != OwnerType.Creature
            ? Task.FromResult<Guid?>(null)
            : context
                .Worlds.Where(world => world.PlayerId == owner.Id)
                .Select(world => (Guid?)world.Id)
                .FirstOrDefaultAsync(cancellationToken);

    private async Task TransferItems(
        InventoryTransferCommand command,
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
                    $"Item {selection.ItemId} not found in {command.From.Type} {command.From.Id}'s inventory."
                );

            if (
                item.Ownership.OwnerId != command.From.Id
                || item.Ownership.OwnerType != command.From.Type
            )
            {
                throw new InvalidOperationException(
                    $"Item {selection.ItemId} is not owned by {command.From.Type} {command.From.Id}."
                );
            }

            if (selection.Quantity <= 0 || selection.Quantity > item.Quantity)
            {
                throw new InvalidOperationException(
                    $"Cannot transfer {selection.Quantity} of item {selection.ItemId}; only {item.Quantity} available."
                );
            }

            if (selection.Quantity < item.Quantity && !ItemStackability.IsStackable(item))
            {
                throw new InvalidOperationException(
                    $"Cannot partially transfer non-stackable item {selection.ItemId}."
                );
            }

            if (item is Gold goldItem)
            {
                await TransferGold(goldItem, command.To, selection.Quantity, cancellationToken);
            }
            else if (selection.Quantity == item.Quantity)
            {
                item.Ownership.OwnerId = command.To.Id;
                item.Ownership.OwnerType = command.To.Type;
                item.Ownership.EquippedSlot = null;
                item.Ownership.AcquiredAt = DateTime.UtcNow;
            }
            else
            {
                item.Quantity -= selection.Quantity;
                context.Items.Add(
                    ItemEquipmentPolicy.Split(
                        item,
                        selection.Quantity,
                        command.To.Id,
                        command.To.Type
                    )
                );
            }
        }

        if (command.From.Type == OwnerType.Creature)
        {
            await RecalculateCreatureAttributes(command.From.Id, cancellationToken);
        }
    }

    private async Task RecalculateCreatureAttributes(
        Guid creatureId,
        CancellationToken cancellationToken
    )
    {
        var fromCreature =
            await context.Creatures.FirstOrDefaultAsync(c => c.Id == creatureId, cancellationToken)
            ?? throw new InvalidOperationException($"Creature {creatureId} not found.");

        var remainingEquipped = await context
            .Items.Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && i.Ownership.OwnerId == creatureId
                && i.Ownership.EquippedSlot != null
            )
            .ToListAsync(cancellationToken);
        CreatureAttributesRecalculator.Recalculate(fromCreature, remainingEquipped);
    }

    private async Task TransferGold(
        Gold sourceGoldItem,
        ItemOwnerReference to,
        int amount,
        CancellationToken cancellationToken
    )
    {
        sourceGoldItem.Quantity -= amount;
        var toGoldItem = await context
            .Items.OfType<Gold>()
            .FirstOrDefaultAsync(
                item => item.Ownership.OwnerId == to.Id && item.Ownership.OwnerType == to.Type,
                cancellationToken
            );
        if (toGoldItem is null)
        {
            toGoldItem = new Gold
            {
                WorldId = sourceGoldItem.WorldId,
                Name = "Gold",
                Ownership = new ItemOwnership { OwnerId = to.Id, OwnerType = to.Type },
            };
            context.Items.Add(toGoldItem);
        }

        toGoldItem.Quantity += amount;
    }
}
