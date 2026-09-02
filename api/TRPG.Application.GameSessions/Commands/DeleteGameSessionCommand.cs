using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.GameSessions.Commands;

public class DeleteGameSessionCommand
{
    public required Guid SessionId { get; init; }
}

internal class DeleteGameSessionCommandHandler(
    IGameSessionsDbContext context,
    IDomainEventPublisher<GameSessionDeletedEvent> gameSessionDeleted
) : ICommandHandler<DeleteGameSessionCommand>
{
    public async Task Handle(
        DeleteGameSessionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );

        await context
            .GameSessions.Where(s => s.Id == command.SessionId)
            .ExecuteDeleteAsync(cancellationToken);
        await gameSessionDeleted.Publish(
            new GameSessionDeletedEvent(command.SessionId),
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);
    }
}
