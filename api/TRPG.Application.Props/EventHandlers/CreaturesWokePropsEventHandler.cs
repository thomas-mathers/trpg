using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Props.Commands;

namespace TRPG.Application.Props.EventHandlers;

internal class CreaturesWokePropsEventHandler(
    ICommandHandler<ClearBedOccupantsCommand> clearBedOccupants
) : IDomainEventConsumer<CreaturesWokeEvent>
{
    public Task Handle(
        CreaturesWokeEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        clearBedOccupants.Handle(
            new ClearBedOccupantsCommand { CreatureIds = domainEvent.CreatureIds },
            cancellationToken
        );
}
