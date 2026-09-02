using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Commands;

public class UnequipInventoryItemCommand
{
    public required Guid CreatureId { get; init; }
    public required EquipmentSlot Slot { get; init; }
}

internal class UnequipInventoryItemCommandHandler(
    IInventoryDbContext context,
    EquipmentLoadoutLoader equipmentLoadoutLoader,
    IDomainEventPublisher<CreatureEquipmentChangedEvent> creatureEquipmentChanged
) : ICommandHandler<UnequipInventoryItemCommand>
{
    public async Task Handle(
        UnequipInventoryItemCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var items = await equipmentLoadoutLoader.LoadOwnedItems(
            command.CreatureId,
            cancellationToken
        );

        var item = items.FirstOrDefault(i => i.Ownership.EquippedSlot == command.Slot);
        if (item == null)
        {
            throw new InvalidOperationException(
                $"No item equipped in slot {command.Slot} for creature {command.CreatureId}."
            );
        }

        item.Ownership.EquippedSlot = null;
        await context.SaveChangesAsync(cancellationToken);

        await creatureEquipmentChanged.Publish(
            new CreatureEquipmentChangedEvent(command.CreatureId),
            cancellationToken
        );
    }
}
