using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;

namespace TRPG.Application.Chat.Commands;

public class ClearChatMessagesCommand
{
    public required Guid SessionId { get; init; }
    public required int KeepFromOrdinal { get; init; }
}

internal class ClearChatMessagesCommandHandler(TrpgDbContext context)
    : ICommandHandler<ClearChatMessagesCommand>
{
    public async Task Handle(
        ClearChatMessagesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .ChatMessages.Where(m =>
                m.SessionId == command.SessionId
                && m.Ordinal > 0
                && m.Ordinal < command.KeepFromOrdinal
            )
            .ExecuteDeleteAsync(cancellationToken);
    }
}
