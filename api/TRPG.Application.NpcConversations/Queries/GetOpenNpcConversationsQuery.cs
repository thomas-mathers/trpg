using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
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
            .GameSessions.AsNoTracking()
            .Where(s => s.Id == query.SessionId)
            .Select(s => s.OpenConversationCreatureIdsByName)
            .FirstOrDefaultAsync(cancellationToken);

        if (openConversations == null)
        {
            throw new EntityNotFoundException("Game session", query.SessionId);
        }

        return openConversations;
    }
}
