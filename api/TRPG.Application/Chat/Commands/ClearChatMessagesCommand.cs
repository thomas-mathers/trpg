using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRPG.Data;

namespace TRPG.Application.Chat.Commands;

internal class ClearChatMessagesCommand
{
    public required Guid SessionId { get; init; }
    public required int KeepFromOrdinal { get; init; }
}

internal class ClearChatMessagesCommandHandler(
    TrpgDbContext context,
    ILogger<ClearChatMessagesCommandHandler> logger
)
{
    public async Task Handle(
        ClearChatMessagesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var deletedCount = await context
            .ChatMessages.Where(m =>
                m.SessionId == command.SessionId
                && m.Ordinal > 0
                && m.Ordinal < command.KeepFromOrdinal
            )
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation(
            "[perf] ClearChatMessages deleted {Count} message(s) in {ElapsedMs}ms",
            deletedCount,
            stopwatch.ElapsedMilliseconds
        );
    }
}
