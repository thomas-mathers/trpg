using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TRPG.Application.Game.Queries;

internal record GameSessionSnapshot(
    Guid WorldId,
    Guid PlayerId,
    TimeSpan Playtime,
    Dictionary<string, Guid> OpenConversationCreatureIdsByName
);

internal class GetGameSessionQuery
{
    public required GameSessionLock Lock { get; init; }
}

internal class GetGameSessionQueryHandler(ILogger<GetGameSessionQueryHandler> logger)
{
    public async Task<GameSessionSnapshot> Handle(
        GetGameSessionQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        await using var context = GameSessionDbContextFactory.Create(query.Lock.Connection);
        var row = await context
            .GameSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.Lock.SessionId, cancellationToken);

        if (row == null)
        {
            throw new GameSessionNotFoundException(query.Lock.SessionId);
        }

        logger.LogInformation(
            "[perf] GetGameSession took {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds
        );

        return new GameSessionSnapshot(
            row.WorldId,
            row.PlayerId,
            row.Playtime,
            row.OpenConversationCreatureIdsByName
        );
    }
}
