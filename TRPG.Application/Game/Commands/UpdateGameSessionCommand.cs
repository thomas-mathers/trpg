using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRPG.Data;

namespace TRPG.Application.Game.Commands;

internal class UpdateGameSessionCommand
{
    public required Guid SessionId { get; init; }
    public TimeSpan? Playtime { get; init; }
    public Dictionary<string, Guid>? OpenConversationCreatureIdsByName { get; init; }
}

internal class UpdateGameSessionCommandHandler(
    TrpgDbContext context,
    ILogger<UpdateGameSessionCommandHandler> logger
)
{
    public async Task Handle(
        UpdateGameSessionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        await context
            .GameSessions.Where(s => s.Id == command.SessionId)
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

        logger.LogInformation(
            "[perf] UpdateGameSession took {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds
        );
    }
}
