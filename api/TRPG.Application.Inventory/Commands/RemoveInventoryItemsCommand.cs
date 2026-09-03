using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Commands;

public record InventoryItemRemoval(Guid CreatureId, Guid ItemId, int Quantity);

public class RemoveInventoryItemsCommand
{
    public required IReadOnlyCollection<InventoryItemRemoval> Removals { get; init; }
}

internal class RemoveInventoryItemsCommandHandler(
    IInventoryDbContext context,
    IDomainEventPublisher<CreatureEquipmentChangedEvent> creatureEquipmentChanged
) : ICommandHandler<RemoveInventoryItemsCommand>
{
    public async Task Handle(
        RemoveInventoryItemsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Removals.Count == 0)
        {
            return;
        }

        var quantitiesByCreatureAndItem = command
            .Removals.GroupBy(removal => (removal.CreatureId, removal.ItemId))
            .ToDictionary(group => group.Key, group => group.Sum(removal => removal.Quantity));

        var creatureIds = quantitiesByCreatureAndItem
            .Keys.Select(key => key.CreatureId)
            .Distinct()
            .ToArray();

        var itemsByCreature = await context
            .Items.Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && creatureIds.AsEnumerable().Contains(i.Ownership.OwnerId)
            )
            .ToListAsync(cancellationToken);
        var itemsByCreatureId = itemsByCreature
            .GroupBy(item => item.Ownership.OwnerId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var ((creatureId, itemId), quantity) in quantitiesByCreatureAndItem)
        {
            var creatureItems = itemsByCreatureId.GetValueOrDefault(creatureId, []);
            var existing = creatureItems.FirstOrDefault(i => i.Id == itemId);
            if (existing == null)
            {
                throw new InvalidOperationException(
                    $"Item {itemId} not found in creature {creatureId}'s inventory."
                );
            }

            if (!existing.CanTrade)
            {
                throw new InvalidOperationException(
                    $"Item {itemId} cannot be traded away right now and cannot be consumed."
                );
            }

            existing.Quantity -= quantity;

            if (existing.Quantity <= 0)
            {
                context.Items.Remove(existing);
                creatureItems.Remove(existing);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var creatureId in creatureIds)
        {
            await creatureEquipmentChanged.Publish(
                new CreatureEquipmentChangedEvent(creatureId),
                cancellationToken
            );
        }
    }
}
