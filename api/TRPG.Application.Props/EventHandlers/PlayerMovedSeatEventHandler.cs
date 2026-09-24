using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Props.Commands;

namespace TRPG.Application.Props.EventHandlers;

internal class PlayerMovedSeatEventHandler(
    ICommandHandler<VacateCreatureSeatCommand> vacateCreatureSeat
) : IDomainEventConsumer<PlayerMovedEvent>
{
    public Task Handle(
        PlayerMovedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        vacateCreatureSeat.Handle(
            new VacateCreatureSeatCommand { CreatureId = domainEvent.PlayerId },
            cancellationToken
        );
}
