using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.CreatureFormulas;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Commands;

public record InventoryItemRemoval(Guid CreatureId, Guid ItemId, int Quantity);

public class RemoveInventoryItemsCommand
{
    public required IReadOnlyCollection<InventoryItemRemoval> Removals { get; init; }
}

internal class RemoveInventoryItemsCommandHandler(TrpgDbContext context)
    : ICommandHandler<RemoveInventoryItemsCommand>
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

        var creaturesById = await context
            .Creatures.Where(c => creatureIds.AsEnumerable().Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var missingCreatureIds = creatureIds.Except(creaturesById.Keys).ToArray();
        if (missingCreatureIds.Length > 0)
        {
            throw new InvalidOperationException($"Creature {missingCreatureIds[0]} not found.");
        }

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

            existing.Quantity -= quantity;

            if (existing.Quantity <= 0)
            {
                context.Items.Remove(existing);
                creatureItems.Remove(existing);
            }
        }

        foreach (var creatureId in creatureIds)
        {
            var equippedItems = itemsByCreatureId
                .GetValueOrDefault(creatureId, [])
                .Where(i => i.Ownership.EquippedSlot != null)
                .ToArray();
            StatFormulas.Recalculate(creaturesById[creatureId], equippedItems);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
