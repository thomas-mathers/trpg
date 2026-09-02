using TRPG.Application.Chat.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;

namespace TRPG.Application.Chat.EventHandlers;

internal sealed class GameSessionDeletedEventHandler(
    ICommandHandler<DeleteChatMessagesBySessionCommand> deleteChatMessagesBySession
) : IDomainEventConsumer<GameSessionDeletedEvent>
{
    public async Task Handle(
        GameSessionDeletedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        await deleteChatMessagesBySession.Handle(
            new DeleteChatMessagesBySessionCommand { SessionId = domainEvent.SessionId },
            cancellationToken
        );
}
