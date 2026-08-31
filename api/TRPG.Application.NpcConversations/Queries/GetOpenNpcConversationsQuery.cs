using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;

namespace TRPG.Application.NpcConversations.Queries;

public class GetOpenNpcConversationsQuery
{
    public required Guid SessionId { get; init; }
}

internal class GetOpenNpcConversationsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetOpenNpcConversationsQuery, Dictionary<string, Guid>>
{
    public async Task<Dictionary<string, Guid>> Handle(
        GetOpenNpcConversationsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var openConversations = await context
            .NpcConversationSessionStates.AsNoTracking()
            .Where(s => s.SessionId == query.SessionId)
            .Select(s => s.OpenConversationCreatureIdsByName)
            .FirstOrDefaultAsync(cancellationToken);

        return openConversations ?? [];
    }
}
