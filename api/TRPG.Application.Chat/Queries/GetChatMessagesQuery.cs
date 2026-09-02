using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Chat.Queries;

public class GetChatMessagesQuery
{
    public required Guid SessionId { get; init; }
}

internal class GetChatMessagesQueryHandler(IChatDbContext context)
    : IQueryHandler<GetChatMessagesQuery, IReadOnlyList<ChatMessage>>
{
    public async Task<IReadOnlyList<ChatMessage>> Handle(
        GetChatMessagesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var rows = await context
            .ChatMessages.AsNoTracking()
            .Where(m => m.SessionId == query.SessionId)
            .OrderBy(m => m.Ordinal)
            .ToArrayAsync(cancellationToken);
        var messages = rows.Select(r =>
                JsonSerializer.Deserialize<ChatMessage>(
                    r.MessageJson,
                    AIJsonUtilities.DefaultOptions
                )!
            )
            .ToArray();

        return messages;
    }
}
