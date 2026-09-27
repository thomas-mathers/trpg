using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.GameSessions.Queries;

public class GetGameTimeQuery
{
    public required Guid SessionId { get; init; }
}

internal class GetGameTimeQueryHandler(IGameSessionsDbContext context, IWorldClock worldClock)
    : IQueryHandler<GetGameTimeQuery, GameInstant>
{
    public async Task<GameInstant> Handle(
        GetGameTimeQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var worldId = await context
            .GameSessions.AsNoTracking()
            .Where(s => s.Id == query.SessionId)
            .Select(s => (Guid?)s.WorldId)
            .SingleOrDefaultAsync(cancellationToken);

        if (worldId == null)
        {
            throw new EntityNotFoundException("Game session", query.SessionId);
        }

        return await worldClock.GetCurrent(worldId.Value, cancellationToken);
    }
}
