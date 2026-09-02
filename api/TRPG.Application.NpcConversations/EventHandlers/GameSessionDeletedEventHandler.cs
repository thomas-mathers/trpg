using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.NpcConversations.Commands;

namespace TRPG.Application.NpcConversations.EventHandlers;

internal sealed class GameSessionDeletedEventHandler(
    ICommandHandler<DeleteNpcConversationSessionStatesBySessionCommand> deleteSessionStates
) : IDomainEventConsumer<GameSessionDeletedEvent>
{
    public async Task Handle(
        GameSessionDeletedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        await deleteSessionStates.Handle(
            new DeleteNpcConversationSessionStatesBySessionCommand
            {
                SessionId = domainEvent.SessionId,
            },
            cancellationToken
        );
}
