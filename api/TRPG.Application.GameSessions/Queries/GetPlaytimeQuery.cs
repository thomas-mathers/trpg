using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.GameSessions.Queries;

public class GetPlaytimeQuery
{
    public required Guid SessionId { get; init; }
}

internal class GetPlaytimeQueryHandler(IGameSessionsDbContext context)
    : IQueryHandler<GetPlaytimeQuery, TimeSpan>
{
    public async Task<TimeSpan> Handle(
        GetPlaytimeQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var playtime = await context
            .GameSessions.AsNoTracking()
            .Where(s => s.Id == query.SessionId)
            .Select(s => (TimeSpan?)s.Playtime)
            .FirstOrDefaultAsync(cancellationToken);

        if (playtime == null)
        {
            throw new EntityNotFoundException("Game session", query.SessionId);
        }

        return playtime.Value;
    }
}
