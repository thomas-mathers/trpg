using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.GameSessions.Queries;

public class GetGameTimeByWorldIdQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetGameTimeByWorldIdQueryHandler(IGameSessionsDbContext context)
    : IQueryHandler<GetGameTimeByWorldIdQuery, GameInstant>
{
    public async Task<GameInstant> Handle(
        GetGameTimeByWorldIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var gameTime = await context
            .GameSessions.AsNoTracking()
            .Where(s => s.WorldId == query.WorldId)
            .Select(s => (GameInstant?)s.GameTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (gameTime == null)
        {
            throw new EntityNotFoundException("Game session", query.WorldId);
        }

        return gameTime.Value;
    }
}
