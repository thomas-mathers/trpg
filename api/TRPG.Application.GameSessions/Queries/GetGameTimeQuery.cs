using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.GameSessions.Queries;

public class GetGameTimeQuery
{
    public required Guid SessionId { get; init; }
}

internal class GetGameTimeQueryHandler(IGameSessionsDbContext context)
    : IQueryHandler<GetGameTimeQuery, GameInstant>
{
    public async Task<GameInstant> Handle(
        GetGameTimeQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var gameTime = await context
            .GameSessions.AsNoTracking()
            .Where(s => s.Id == query.SessionId)
            .Select(s => (GameInstant?)s.GameTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (gameTime == null)
        {
            throw new EntityNotFoundException("Game session", query.SessionId);
        }

        return gameTime.Value;
    }
}
