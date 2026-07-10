using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.Conversations.Queries;

internal class GetConversationSummaryQuery
{
    public required Guid CreatureId { get; init; }
    public required Guid NpcId { get; init; }
}

internal class GetConversationSummaryQueryHandler(TrpgDbContext context)
{
    public async Task<string> Handle(
        GetConversationSummaryQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var conversation = await context
            .NpcConversations.AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.CreatureId == query.CreatureId && c.NpcId == query.NpcId,
                cancellationToken
            );

        return conversation?.Summary ?? "";
    }
}
