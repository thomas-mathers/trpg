using Microsoft.EntityFrameworkCore;
using TRPG.Application.Chat.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.NpcConversations.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.GameSessions.Commands;

public class DeleteGameSessionCommand
{
    public required Guid SessionId { get; init; }
}

internal class DeleteGameSessionCommandHandler(
    IGameSessionsDbContext context,
    ICommandHandler<DeleteChatMessagesBySessionCommand> deleteChatMessagesBySession,
    ICommandHandler<DeleteNpcConversationSessionStatesBySessionCommand> deleteNpcConversationSessionStatesBySession
) : ICommandHandler<DeleteGameSessionCommand>
{
    public async Task Handle(
        DeleteGameSessionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );

        await deleteChatMessagesBySession.Handle(
            new DeleteChatMessagesBySessionCommand { SessionId = command.SessionId },
            cancellationToken
        );
        await deleteNpcConversationSessionStatesBySession.Handle(
            new DeleteNpcConversationSessionStatesBySessionCommand
            {
                SessionId = command.SessionId,
            },
            cancellationToken
        );
        await context
            .GameSessions.Where(s => s.Id == command.SessionId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
