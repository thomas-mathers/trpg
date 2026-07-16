using Microsoft.EntityFrameworkCore;

namespace TRPG.Application.Game.Queries;

internal class GetOpenConversationsQuery
{
    public required GameSessionLock Lock { get; init; }
}

internal class GetOpenConversationsQueryHandler
{
    public async Task<Dictionary<string, Guid>> Handle(
        GetOpenConversationsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = GameSessionDbContextFactory.Create(query.Lock.Connection);
        var openConversations = await context
            .GameSessions.AsNoTracking()
            .Where(s => s.Id == query.Lock.SessionId)
            .Select(s => s.OpenConversationCreatureIdsByName)
            .FirstOrDefaultAsync(cancellationToken);

        if (openConversations == null)
        {
            throw new GameSessionNotFoundException(query.Lock.SessionId);
        }

        return openConversations;
    }
}
