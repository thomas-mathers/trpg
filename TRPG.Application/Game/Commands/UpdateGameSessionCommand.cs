using Microsoft.EntityFrameworkCore;

namespace TRPG.Application.Game.Commands;

internal class UpdateGameSessionCommand
{
    public required GameSessionLock Lock { get; init; }
    public TimeSpan? Playtime { get; init; }
    public Dictionary<string, Guid>? OpenConversationCreatureIdsByName { get; init; }
}

internal class UpdateGameSessionCommandHandler
{
    public async Task Handle(
        UpdateGameSessionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = GameSessionDbContextFactory.Create(command.Lock.Connection);
        await context
            .GameSessions.Where(s => s.Id == command.Lock.SessionId)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(s => s.Playtime, s => command.Playtime ?? s.Playtime)
                        .SetProperty(
                            s => s.OpenConversationCreatureIdsByName,
                            s =>
                                command.OpenConversationCreatureIdsByName
                                ?? s.OpenConversationCreatureIdsByName
                        ),
                cancellationToken
            );
    }
}
