using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Commands;

public record InventoryItemTransferResult(Guid SourceItemId, Guid DestinationItemId, int Quantity);

public class TransferInventoryItemsCommand
{
    public required ItemOwnerReference From { get; init; }
    public required ItemOwnerReference To { get; init; }
    public required IReadOnlyList<ItemSelection> Items { get; init; }
}

internal class TransferInventoryItemsCommandHandler(
    TrpgDbContext context,
    ICommandHandler<AddGoldCommand> addGold,
    IDomainEventPublisher<CreatureEquipmentChangedEvent> creatureEquipmentChanged
) : ICommandHandler<TransferInventoryItemsCommand, IReadOnlyCollection<InventoryItemTransferResult>>
{
    public async Task<IReadOnlyCollection<InventoryItemTransferResult>> Handle(
        TransferInventoryItemsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var transferItems = await InventoryTransferValidation.GetValidatedTransferItems(
            command.From,
            command.Items,
            context.Items,
            cancellationToken
        );
        await EnsureDestinationCapacity(command.To, transferItems, cancellationToken);
        var results = await MoveItems(transferItems, command.To, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await RecalculateSourceAttributes(command.From, cancellationToken);
        return results;
    }

    private async Task EnsureDestinationCapacity(
        ItemOwnerReference to,
        IReadOnlyCollection<TransferItem> transferItems,
        CancellationToken cancellationToken
    )
    {
        if (to.Type != OwnerType.Creature)
        {
            return;
        }

        var creature =
            await context
                .Creatures.AsNoTracking()
                .FirstOrDefaultAsync(creature => creature.Id == to.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Creature {to.Id} not found.");

        if (creature.State == CreatureState.Dead)
        {
            return;
        }

        var currentWeight = await context
            .Items.Where(item =>
                item.Ownership.OwnerType == OwnerType.Creature && item.Ownership.OwnerId == to.Id
            )
            .SumAsync(item => item.Weight * item.Quantity, cancellationToken);

        var incomingWeight = transferItems.Sum(transferItem =>
            transferItem.Item.Weight * transferItem.Quantity
        );

        if (currentWeight + incomingWeight > creature.CarryingCapacity)
        {
            throw new InvalidOperationException(
                $"Creature {to.Id} cannot carry any more weight; carrying capacity is {creature.CarryingCapacity}."
            );
        }
    }

    private async Task<IReadOnlyCollection<InventoryItemTransferResult>> MoveItems(
        IReadOnlyCollection<TransferItem> transferItems,
        ItemOwnerReference to,
        CancellationToken cancellationToken
    )
    {
        var results = new List<InventoryItemTransferResult>();

        foreach (var (item, quantity) in transferItems)
        {
            if (item is Gold goldItem)
            {
                goldItem.Quantity -= quantity;
                await addGold.Handle(
                    new AddGoldCommand
                    {
                        Owner = to,
                        WorldId = goldItem.WorldId,
                        Amount = quantity,
                    },
                    cancellationToken
                );
                var destinationGold = await context
                    .Items.OfType<Gold>()
                    .FirstAsync(
                        candidate =>
                            candidate.Ownership.OwnerId == to.Id
                            && candidate.Ownership.OwnerType == to.Type,
                        cancellationToken
                    );
                results.Add(new(item.Id, destinationGold.Id, quantity));
            }
            else if (quantity == item.Quantity)
            {
                item.Ownership.OwnerId = to.Id;
                item.Ownership.OwnerType = to.Type;
                item.Ownership.EquippedSlot = null;
                item.Ownership.AcquiredAt = DateTime.UtcNow;
                results.Add(new(item.Id, item.Id, quantity));
            }
            else
            {
                item.Quantity -= quantity;
                var splitItem = Split(item, quantity, to);
                context.Items.Add(splitItem);
                results.Add(new(item.Id, splitItem.Id, quantity));
            }
        }

        return results;
    }

    private static Item Split(Item item, int quantity, ItemOwnerReference owner)
    {
        var type = item.GetType();
        var node = JsonSerializer.SerializeToNode(item, type)!.AsObject();
        node[nameof(Item.Id)] = Guid.NewGuid();
        node[nameof(Item.Quantity)] = quantity;
        node[nameof(Item.Ownership)] = JsonSerializer.SerializeToNode(
            new ItemOwnership { OwnerId = owner.Id, OwnerType = owner.Type }
        );
        return (Item)node.Deserialize(type)!;
    }

    private async Task RecalculateSourceAttributes(
        ItemOwnerReference from,
        CancellationToken cancellationToken
    )
    {
        if (from.Type == OwnerType.Creature)
        {
            await creatureEquipmentChanged.Publish(
                new CreatureEquipmentChangedEvent(from.Id),
                cancellationToken
            );
        }
    }
}
