using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.NpcConversations.Queries;

public class GetNpcConversationSummaryQuery
{
    public required Guid CreatureId { get; init; }
    public required Guid NpcId { get; init; }
}

internal class GetNpcConversationSummaryQueryHandler(INpcConversationsDbContext context)
    : IQueryHandler<GetNpcConversationSummaryQuery, string>
{
    public async Task<string> Handle(
        GetNpcConversationSummaryQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
                .NpcConversationHistories.AsNoTracking()
                .Where(c => c.CreatureId == query.CreatureId && c.NpcId == query.NpcId)
                .Select(c => c.Summary)
                .FirstOrDefaultAsync(cancellationToken)
            ?? "";
    }
}
