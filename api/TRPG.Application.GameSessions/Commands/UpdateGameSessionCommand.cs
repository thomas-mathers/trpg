using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;

namespace TRPG.Application.GameSessions.Commands;

public class UpdateGameSessionCommand
{
    public required Guid SessionId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class UpdateGameSessionCommandHandler(IGameSessionsDbContext context)
    : ICommandHandler<UpdateGameSessionCommand>
{
    public async Task Handle(
        UpdateGameSessionCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .GameSessions.Where(s => s.Id == command.SessionId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.GameTime, command.GameTime),
                cancellationToken
            );
}
