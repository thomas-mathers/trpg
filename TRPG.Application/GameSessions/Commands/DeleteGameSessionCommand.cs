using Microsoft.EntityFrameworkCore;
using TRPG.Data;

namespace TRPG.Application.GameSessions.Commands;

internal class DeleteGameSessionCommand
{
    public required Guid SessionId { get; init; }
}

internal class DeleteGameSessionCommandHandler(TrpgDbContext context)
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
