using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.Worlds.EventHandlers;

internal sealed class PlayerMovedDungeonPremiseEventHandler(
    ICommandHandler<EnsureDungeonPremiseCommand> ensureDungeonPremise
) : IDomainEventConsumer<PlayerMovedEvent>
{
    public Task Handle(
        PlayerMovedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        ensureDungeonPremise.Handle(
            new EnsureDungeonPremiseCommand { RoomLocationId = domainEvent.ToLocationId },
            cancellationToken
        );
}
