using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.LocationSimulation.Commands;

namespace TRPG.Application.LocationSimulation.EventHandlers;

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
                Playtime = domainEvent.Playtime,
            },
            cancellationToken
        );
}
