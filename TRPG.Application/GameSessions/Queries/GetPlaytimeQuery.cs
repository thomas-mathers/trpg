using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRPG.Application.GameSessions.Exceptions;
using TRPG.Data;

namespace TRPG.Application.GameSessions.Queries;

internal class GetPlaytimeQuery
{
    public required Guid SessionId { get; init; }
}

internal class GetPlaytimeQueryHandler(
    TrpgDbContext context,
    ILogger<GetPlaytimeQueryHandler> logger
)
{
    public async Task<TimeSpan> Handle(
        GetPlaytimeQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var playtime = await context
            .GameSessions.AsNoTracking()
            .Where(s => s.Id == query.SessionId)
            .Select(s => (TimeSpan?)s.Playtime)
            .FirstOrDefaultAsync(cancellationToken);

        if (playtime == null)
        {
            throw new GameSessionNotFoundException(query.SessionId);
        }

        logger.LogInformation(
            "[perf] GetPlaytime took {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds
        );

        return playtime.Value;
    }
}
