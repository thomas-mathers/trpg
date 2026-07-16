using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TRPG.Application.Game.Commands;

internal class ClearChatMessagesCommand
{
    public required GameSessionLock Lock { get; init; }
    public required int KeepFromOrdinal { get; init; }
}

internal class ClearChatMessagesCommandHandler(ILogger<ClearChatMessagesCommandHandler> logger)
{
    public async Task Handle(
        ClearChatMessagesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        await using var context = GameSessionDbContextFactory.Create(command.Lock.Connection);
        var deletedCount = await context
            .ChatMessages.Where(m =>
                m.SessionId == command.Lock.SessionId
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
