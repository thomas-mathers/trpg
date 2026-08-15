using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;

namespace TRPG.Application.GameSessions.Commands;

public class UpdateGameSessionCommand
{
    public required Guid SessionId { get; init; }
    public TimeSpan? Playtime { get; init; }
    public Dictionary<string, Guid>? OpenConversationCreatureIdsByName { get; init; }
}

public class UpdateGameSessionCommandHandler(TrpgDbContext context)
    : ICommandHandler<UpdateGameSessionCommand>
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
    }
}
