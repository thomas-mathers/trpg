using Microsoft.EntityFrameworkCore;

namespace TRPG.Application.Game.Commands;

internal class DeleteGameSessionCommand
{
    public required GameSessionLock Lock { get; init; }
}

internal class DeleteGameSessionCommandHandler
{
    public async Task Handle(
        DeleteGameSessionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = GameSessionDbContextFactory.Create(command.Lock.Connection);
        await context
            .ChatMessages.Where(m => m.SessionId == command.Lock.SessionId)
            .ExecuteDeleteAsync(cancellationToken);
        await context
            .Combatants.Where(c => c.SessionId == command.Lock.SessionId)
            .ExecuteDeleteAsync(cancellationToken);
        await context
            .GameSessions.Where(s => s.Id == command.Lock.SessionId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
