using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Inventory.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.EventHandlers;

internal sealed class QuestGoldRewardedEventHandler(ICommandHandler<AddGoldCommand> addGold)
    : IDomainEventConsumer<QuestGoldRewardedEvent>
{
    public async Task Handle(
        QuestGoldRewardedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        await addGold.Handle(
            new AddGoldCommand
            {
                Owner = new ItemOwnerReference(domainEvent.PlayerId, OwnerType.Creature),
                WorldId = domainEvent.WorldId,
                Amount = domainEvent.Amount,
            },
            cancellationToken
        );
}
