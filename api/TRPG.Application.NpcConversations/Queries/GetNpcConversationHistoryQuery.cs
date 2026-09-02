using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.NpcConversations.Queries;

public class GetNpcConversationHistoryQuery
{
    public required Guid CreatureId { get; init; }
    public required Guid NpcId { get; init; }
}

internal class GetNpcConversationHistoryQueryHandler(INpcConversationsDbContext context)
    : IQueryHandler<GetNpcConversationHistoryQuery, NpcConversationHistory?>
{
    public async Task<NpcConversationHistory?> Handle(
        GetNpcConversationHistoryQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .NpcConversationHistories.AsNoTracking()
            .FirstOrDefaultAsync(
                history => history.CreatureId == query.CreatureId && history.NpcId == query.NpcId,
                cancellationToken
            );
}
