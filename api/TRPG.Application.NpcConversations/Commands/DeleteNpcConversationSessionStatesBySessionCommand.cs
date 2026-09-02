using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.NpcConversations.Commands;

public class DeleteNpcConversationSessionStatesBySessionCommand
{
    public required Guid SessionId { get; init; }
}

internal class DeleteNpcConversationSessionStatesBySessionCommandHandler(
    INpcConversationsDbContext context
) : ICommandHandler<DeleteNpcConversationSessionStatesBySessionCommand>
{
    public async Task Handle(
        DeleteNpcConversationSessionStatesBySessionCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .NpcConversationSessionStates.Where(s => s.SessionId == command.SessionId)
            .ExecuteDeleteAsync(cancellationToken);
}
