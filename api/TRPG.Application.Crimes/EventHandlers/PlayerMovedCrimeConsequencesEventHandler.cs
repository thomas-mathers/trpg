using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Crimes.Commands;

namespace TRPG.Application.Crimes.EventHandlers;

internal sealed class PlayerMovedCrimeConsequencesEventHandler(
    ICommandHandler<ResolveCrimeConsequencesAtLocationCommand> resolveCrimeConsequencesAtLocation
) : IDomainEventConsumer<PlayerMovedEvent>
{
    public Task Handle(
        PlayerMovedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        resolveCrimeConsequencesAtLocation.Handle(
            new ResolveCrimeConsequencesAtLocationCommand
            {
                WorldId = domainEvent.WorldId,
                PlayerId = domainEvent.PlayerId,
                LocationId = domainEvent.FromLocationId,
            },
            cancellationToken
        );
}
