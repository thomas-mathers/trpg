using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.NpcConversations.Queries;

public class GetRecentNpcConversationsQuery
{
    public required Guid NpcConversationHistoryId { get; init; }
    public required int Limit { get; init; }
}

internal class GetRecentNpcConversationsQueryHandler(INpcConversationsDbContext context)
    : IQueryHandler<GetRecentNpcConversationsQuery, IReadOnlyList<NpcConversation>>
{
    public async Task<IReadOnlyList<NpcConversation>> Handle(
        GetRecentNpcConversationsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var mostRecentFirst = await context
            .NpcConversations.AsNoTracking()
            .Where(conversation =>
                conversation.NpcConversationHistoryId == query.NpcConversationHistoryId
            )
            .OrderByDescending(conversation => conversation.CreatedAt)
            .Take(query.Limit)
            .ToArrayAsync(cancellationToken);

        return mostRecentFirst.OrderBy(conversation => conversation.CreatedAt).ToArray();
    }
}
