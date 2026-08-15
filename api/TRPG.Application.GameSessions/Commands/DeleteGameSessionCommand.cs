using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;

namespace TRPG.Application.GameSessions.Commands;

public class DeleteGameSessionCommand
{
    public required Guid SessionId { get; init; }
}

public class DeleteGameSessionCommandHandler(TrpgDbContext context)
    : ICommandHandler<DeleteGameSessionCommand>
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
            .ChatMessages.Where(m => m.SessionId == command.SessionId)
            .ExecuteDeleteAsync(cancellationToken);
        await context
            .GameSessions.Where(s => s.Id == command.SessionId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
