using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;

namespace TRPG.Application.NpcConversations.Queries;

public class GetNpcConversationSummaryQuery
{
    public required Guid CreatureId { get; init; }
    public required Guid NpcId { get; init; }
}

internal class GetNpcConversationSummaryQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetNpcConversationSummaryQuery, string>
{
    public async Task<string> Handle(
        GetNpcConversationSummaryQuery query,
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
