using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Knowledge.Commands;

namespace TRPG.Application.Knowledge.EventHandlers;

internal sealed class PlayerMovedRoomVisitEventHandler(
    ICommandHandler<RecordRoomVisitCommand> recordRoomVisit
) : IDomainEventConsumer<PlayerMovedEvent>
{
    public Task Handle(
        PlayerMovedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        recordRoomVisit.Handle(
            new RecordRoomVisitCommand
            {
                WorldId = domainEvent.WorldId,
                CreatureId = domainEvent.PlayerId,
                RoomLocationId = domainEvent.ToLocationId,
            },
            cancellationToken
        );
}
