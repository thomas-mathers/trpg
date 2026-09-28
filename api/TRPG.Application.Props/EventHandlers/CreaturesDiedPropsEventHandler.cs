using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Props.Commands;

namespace TRPG.Application.Props.EventHandlers;

internal class CreaturesDiedPropsEventHandler(
    ICommandHandler<ClearSeatOccupantsCommand> clearSeatOccupants,
    ICommandHandler<ClearBedOccupantsCommand> clearBedOccupants,
    ICommandHandler<ClearWorkstationOccupantsCommand> clearWorkstationOccupants
) : IDomainEventConsumer<CreaturesDiedEvent>
{
    public async Task Handle(
        CreaturesDiedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        await clearSeatOccupants.Handle(
            new ClearSeatOccupantsCommand { CreatureIds = domainEvent.CreatureIds },
            cancellationToken
        );
        await clearBedOccupants.Handle(
            new ClearBedOccupantsCommand { CreatureIds = domainEvent.CreatureIds },
            cancellationToken
        );
        await clearWorkstationOccupants.Handle(
            new ClearWorkstationOccupantsCommand { CreatureIds = domainEvent.CreatureIds },
            cancellationToken
        );
    }
}
