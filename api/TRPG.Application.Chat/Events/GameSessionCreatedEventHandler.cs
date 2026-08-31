using Microsoft.Extensions.AI;
using TRPG.Application.Chat.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;

namespace TRPG.Application.Chat.Events;

internal sealed class GameSessionCreatedEventHandler(
    ICommandHandler<AppendChatMessagesCommand, int> appendChatMessages
) : IDomainEventConsumer<GameSessionCreatedEvent>
{
    public async Task Handle(
        GameSessionCreatedEvent domainEvent,
        CancellationToken cancellationToken = default
    ) =>
        await appendChatMessages.Handle(
            new AppendChatMessagesCommand
            {
                SessionId = domainEvent.SessionId,
                Messages = [new ChatMessage(ChatRole.System, domainEvent.SystemPrompt)],
            },
            cancellationToken
        );
}
