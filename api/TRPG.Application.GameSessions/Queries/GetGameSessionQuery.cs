using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.GameSessions.Queries;

public class GetGameSessionQuery
{
    public required Guid SessionId { get; init; }
}

internal class GetGameSessionQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetGameSessionQuery, GameSession>
{
    public async Task<GameSession> Handle(
        GetGameSessionQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var row = await context
            .GameSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.SessionId, cancellationToken);

        if (row == null)
        {
            throw new EntityNotFoundException("Game session", query.SessionId);
        }

        return row;
    }
}
