using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.WorldSimulation.Commands;

namespace TRPG.Application.WorldSimulation.EventHandlers;

internal sealed class PlayerMovedAlertedCreatureResetEventHandler(
    ICommandHandler<ResetAlertedCreaturesCommand> resetAlertedCreatures
) : IDomainEventConsumer<PlayerMovedEvent>
{
    public Task Handle(
        PlayerMovedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        resetAlertedCreatures.Handle(
            new ResetAlertedCreaturesCommand
            {
                WorldId = domainEvent.WorldId,
                LocationId = domainEvent.FromLocationId,
            },
            cancellationToken
        );
}
