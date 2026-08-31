using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Events;
using TRPG.Application.CreatureFormulas;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.EventHandlers;

internal sealed class CreatureEquipmentChangedEventHandler(TrpgDbContext context)
    : IDomainEventConsumer<CreatureEquipmentChangedEvent>
{
    public async Task Handle(
        CreatureEquipmentChangedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await context.Creatures.FirstAsync(
            c => c.Id == domainEvent.CreatureId,
            cancellationToken
        );

        var equippedItems = await context
            .Items.Where(i =>
                i.Ownership.OwnerType == OwnerType.Creature
                && i.Ownership.OwnerId == domainEvent.CreatureId
                && i.Ownership.EquippedSlot != null
            )
            .ToListAsync(cancellationToken);

        StatFormulas.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);
    }
}
