using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Data;

namespace TRPG.Application.GameSessions.Queries;

internal class GetOpenConversationsQuery
{
    public required Guid SessionId { get; init; }
}

internal class GetOpenConversationsQueryHandler(TrpgDbContext context)
{
    public async Task<Dictionary<string, Guid>> Handle(
        GetOpenConversationsQuery query,
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
