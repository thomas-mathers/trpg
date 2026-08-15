using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Handling;
using TRPG.Data;

namespace TRPG.Application.Chat.Queries;

public class GetChatMessagesQuery
{
    public required Guid SessionId { get; init; }
}

public class GetChatMessagesQueryHandler(
    TrpgDbContext context,
    ILogger<GetChatMessagesQueryHandler> logger
) : IQueryHandler<GetChatMessagesQuery, IReadOnlyList<ChatMessage>>
{
    public async Task<IReadOnlyList<ChatMessage>> Handle(
        GetChatMessagesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var rows = await context
            .ChatMessages.AsNoTracking()
            .Where(m => m.SessionId == query.SessionId)
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
