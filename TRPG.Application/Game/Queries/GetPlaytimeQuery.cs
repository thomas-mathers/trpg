using Microsoft.EntityFrameworkCore;

namespace TRPG.Application.Game.Queries;

internal class GetPlaytimeQuery
{
    public required GameSessionLock Lock { get; init; }
}

internal class GetPlaytimeQueryHandler
{
    public async Task<TimeSpan> Handle(
        GetPlaytimeQuery query,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = GameSessionDbContextFactory.Create(query.Lock.Connection);
        var playtime = await context
            .GameSessions.AsNoTracking()
            .Where(s => s.Id == query.Lock.SessionId)
            .Select(s => (TimeSpan?)s.Playtime)
            .FirstOrDefaultAsync(cancellationToken);

        if (playtime == null)
        {
            throw new GameSessionNotFoundException(query.Lock.SessionId);
        }

        return playtime.Value;
    }
}
