using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Chat.Commands;

public class RemoveChatMessagesFromOrdinalCommand
{
    public required Guid SessionId { get; init; }
    public required int FromOrdinal { get; init; }
}

internal class RemoveChatMessagesFromOrdinalCommandHandler(IChatDbContext context)
    : ICommandHandler<RemoveChatMessagesFromOrdinalCommand>
{
    public async Task Handle(
        RemoveChatMessagesFromOrdinalCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .ChatMessages.Where(m =>
                m.SessionId == command.SessionId && m.Ordinal >= command.FromOrdinal
            )
            .ExecuteDeleteAsync(cancellationToken);
}
