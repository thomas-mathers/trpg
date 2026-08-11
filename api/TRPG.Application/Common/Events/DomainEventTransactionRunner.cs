using TRPG.Data;

namespace TRPG.Application.Common.Events;

internal class DomainEventTransactionRunner(
    TrpgDbContext context,
    IEnumerable<GameEventListener> listeners
)
{
    public async Task<TResult> Run<TInput, TResult>(
        TInput input,
        Func<TInput, CancellationToken, Task<GameActionResult<TResult>>> action,
        CancellationToken cancellationToken = default
    )
    {
        if (context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "DomainEventTransactionRunner must own the transaction it commits."
            );
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        var actionResult = await action(input, cancellationToken);
        await Dispatch(actionResult.Events, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return actionResult.Result;
    }

    private async Task Dispatch(
        IReadOnlyCollection<GameEvent> events,
        CancellationToken cancellationToken
    )
    {
        foreach (var gameEvent in events)
        {
            foreach (var listener in listeners)
            {
                await listener.Handle(gameEvent, cancellationToken);
            }
        }
    }
}
