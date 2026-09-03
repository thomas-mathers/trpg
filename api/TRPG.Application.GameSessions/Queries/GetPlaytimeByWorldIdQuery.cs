using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.GameSessions.Queries;

public class GetPlaytimeByWorldIdQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetPlaytimeByWorldIdQueryHandler(IGameSessionsDbContext context)
    : IQueryHandler<GetPlaytimeByWorldIdQuery, TimeSpan>
{
    public async Task<TimeSpan> Handle(
        GetPlaytimeByWorldIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var playtime = await context
            .GameSessions.AsNoTracking()
            .Where(s => s.WorldId == query.WorldId)
            .Select(s => (TimeSpan?)s.Playtime)
            .FirstOrDefaultAsync(cancellationToken);

        if (playtime == null)
        {
            throw new EntityNotFoundException("Game session", query.WorldId);
        }

        return playtime.Value;
    }
}
