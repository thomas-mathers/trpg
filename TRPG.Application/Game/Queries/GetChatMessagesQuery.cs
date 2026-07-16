using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace TRPG.Application.Game.Queries;

internal class GetChatMessagesQuery
{
    public required GameSessionLock Lock { get; init; }
}

internal class GetChatMessagesQueryHandler(ILogger<GetChatMessagesQueryHandler> logger)
{
    public async Task<IReadOnlyList<ChatMessage>> Handle(
        GetChatMessagesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        await using var context = GameSessionDbContextFactory.Create(query.Lock.Connection);
        var rows = await context
            .ChatMessages.AsNoTracking()
            .Where(m => m.SessionId == query.Lock.SessionId)
            .OrderBy(m => m.Ordinal)
            .ToArrayAsync(cancellationToken);
        var queryMs = stopwatch.ElapsedMilliseconds;

        var messages = rows.Select(r =>
                JsonSerializer.Deserialize<ChatMessage>(
                    r.MessageJson,
                    AIJsonUtilities.DefaultOptions
                )!
            )
            .ToArray();

        logger.LogInformation(
            "[perf] GetChatMessages fetched {RowCount} rows in {QueryMs}ms, deserialized in {DeserializeMs}ms",
            rows.Length,
            queryMs,
            stopwatch.ElapsedMilliseconds - queryMs
        );

        return messages;
    }
}
