using TRPG.Data;

namespace TRPG.Application.Common.Events;

internal class DomainEventTransactionRunner(
    TrpgDbContext context,
    IEnumerable<GameDomainEventListener> listeners
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
        await Dispatch(actionResult.DomainEvents, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return actionResult.Result;
    }

    private async Task Dispatch(
        IReadOnlyCollection<GameDomainEvent> domainEvents,
        CancellationToken cancellationToken
    )
    {
        foreach (var domainEvent in domainEvents)
        {
            foreach (var listener in listeners)
            {
                await listener.Handle(domainEvent, cancellationToken);
            }
        }
    }
}
