using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.GameSessions.Commands;

public class AdvanceTimeCommand
{
    public required Guid SessionId { get; init; }
    public required TimeSpan Delta { get; init; }
}

internal class AdvanceTimeCommandHandler(IGameSessionsDbContext context, IWorldClock worldClock)
    : ICommandHandler<AdvanceTimeCommand, GameInstant>
{
    public async Task<GameInstant> Handle(
        AdvanceTimeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var worldId = await context
            .GameSessions.AsNoTracking()
            .Where(session => session.Id == command.SessionId)
            .Select(session => (Guid?)session.WorldId)
            .SingleOrDefaultAsync(cancellationToken);
        if (worldId == null)
        {
            throw new EntityNotFoundException("Game session", command.SessionId);
        }

        return await worldClock.Advance(worldId.Value, command.Delta, cancellationToken);
    }
}
