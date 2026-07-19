using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRPG.Data;

namespace TRPG.Application.GameSessions.Commands;

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
        if (command.Playtime == null && command.OpenConversationCreatureIdsByName == null)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        await context
            .GameSessions.Where(s => s.Id == command.SessionId)
            .ExecuteUpdateAsync(
                setters =>
                {
                    if (command.Playtime != null)
                    {
                        setters.SetProperty(s => s.Playtime, command.Playtime.Value);
                    }
                    if (command.OpenConversationCreatureIdsByName != null)
                    {
                        setters.SetProperty(
                            s => s.OpenConversationCreatureIdsByName,
                            command.OpenConversationCreatureIdsByName
                        );
                    }
                },
                cancellationToken
            );

        logger.LogInformation(
            "[perf] UpdateGameSession took {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds
        );
    }
}
