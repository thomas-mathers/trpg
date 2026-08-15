using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.GameSessions.Queries;

public class GetGameSessionQuery
{
    public required Guid SessionId { get; init; }
}

public class GetGameSessionQueryHandler(
    TrpgDbContext context,
    ILogger<GetGameSessionQueryHandler> logger
) : IQueryHandler<GetGameSessionQuery, GameSession>
{
    public async Task<GameSession> Handle(
        GetGameSessionQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var row = await context
            .GameSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.SessionId, cancellationToken);

        if (row == null)
        {
            throw new EntityNotFoundException("Game session", query.SessionId);
        }

        logger.LogInformation(
            "[perf] GetGameSession took {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds
        );

        return row;
    }
}
