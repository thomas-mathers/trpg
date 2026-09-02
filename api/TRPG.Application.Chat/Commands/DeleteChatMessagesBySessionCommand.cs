using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Chat.Commands;

public class DeleteChatMessagesBySessionCommand
{
    public required Guid SessionId { get; init; }
}

internal class DeleteChatMessagesBySessionCommandHandler(IChatDbContext context)
    : ICommandHandler<DeleteChatMessagesBySessionCommand>
{
    public async Task Handle(
        DeleteChatMessagesBySessionCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .ChatMessages.Where(m => m.SessionId == command.SessionId)
            .ExecuteDeleteAsync(cancellationToken);
}
