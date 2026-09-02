using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Commands;

public class EquipInventoryItemCommand
{
    public required Guid CreatureId { get; init; }
    public required Guid ItemId { get; init; }
    public required EquipmentSlot Slot { get; init; }
}

internal class EquipInventoryItemCommandHandler(
    IInventoryDbContext context,
    EquipmentLoadoutLoader equipmentLoadoutLoader,
    IDomainEventPublisher<CreatureEquipmentChangedEvent> creatureEquipmentChanged
) : ICommandHandler<EquipInventoryItemCommand>
{
    public async Task Handle(
        EquipInventoryItemCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var items = await equipmentLoadoutLoader.LoadOwnedItems(
            command.CreatureId,
            cancellationToken
        );

        var toEquip = items.FirstOrDefault(i => i.Id == command.ItemId);
        if (toEquip == null)
        {
            throw new InvalidOperationException(
                $"Item {command.ItemId} not found in creature {command.CreatureId}'s inventory."
            );
        }

        if (EquipmentLoadoutPolicy.GetDefaultSlot(toEquip) == null)
        {
            throw new InvalidOperationException($"Item {command.ItemId} cannot be equipped.");
        }

        await UnequipConflictingItems(toEquip, command.Slot, items, cancellationToken);

        toEquip.Ownership.EquippedSlot = EquipmentLoadoutPolicy.ResolveEquippedSlot(
            toEquip,
            command.Slot
        );
        await context.SaveChangesAsync(cancellationToken);

        await creatureEquipmentChanged.Publish(
            new CreatureEquipmentChangedEvent(command.CreatureId),
            cancellationToken
        );
    }

    private async Task UnequipConflictingItems(
        Item toEquip,
        EquipmentSlot slot,
        IReadOnlyCollection<Item> items,
        CancellationToken cancellationToken
    )
    {
        var currentlyEquipped = items.Where(i => i.Ownership.EquippedSlot != null).ToArray();
        var conflicting = EquipmentLoadoutPolicy.GetConflictingItems(
            toEquip,
            slot,
            currentlyEquipped
        );

        if (conflicting.Count == 0)
        {
            return;
        }

        foreach (var item in conflicting)
        {
            item.Ownership.EquippedSlot = null;
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
