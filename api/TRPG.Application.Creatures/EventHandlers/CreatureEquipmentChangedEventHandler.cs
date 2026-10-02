using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.EventHandlers;

internal sealed class CreatureEquipmentChangedEventHandler(
    ICreaturesDbContext context,
    IQueryHandler<GetInventoryItemsByOwnerQuery, IReadOnlyList<Item>> getInventoryItemsByOwner,
    IOptionsSnapshot<CreatureGeneratorOptions> optionsSnapshot
) : IDomainEventConsumer<CreatureEquipmentChangedEvent>
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

        var ownedItems = await getInventoryItemsByOwner.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(domainEvent.CreatureId, OwnerType.Creature),
            },
            cancellationToken
        );
        var equippedItems = ownedItems.Where(item => item.Ownership.EquippedSlot != null).ToList();

        StatFormulas.RefreshMovementSpeed(creature, equippedItems, optionsSnapshot.Value);
        StatFormulas.Recalculate(creature, equippedItems);

        await context.SaveChangesAsync(cancellationToken);
    }
}
