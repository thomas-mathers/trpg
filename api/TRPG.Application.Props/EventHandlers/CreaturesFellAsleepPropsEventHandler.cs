using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Props.Commands;

namespace TRPG.Application.Props.EventHandlers;

internal class CreaturesFellAsleepPropsEventHandler(
    ICommandHandler<ClearSeatOccupantsCommand> clearSeatOccupants,
    ICommandHandler<ClearWorkstationOccupantsCommand> clearWorkstationOccupants
) : IDomainEventConsumer<CreaturesFellAsleepEvent>
{
    public async Task Handle(
        CreaturesFellAsleepEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        await clearSeatOccupants.Handle(
            new ClearSeatOccupantsCommand { CreatureIds = domainEvent.CreatureIds },
            cancellationToken
        );
        await clearWorkstationOccupants.Handle(
            new ClearWorkstationOccupantsCommand { CreatureIds = domainEvent.CreatureIds },
            cancellationToken
        );
    }
}
