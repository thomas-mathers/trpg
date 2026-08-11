using TRPG.Application.GameSessions;
using TRPG.Data;

namespace TRPG.Application.Common.Events;

internal class DomainEventTransactionRunner(
    TrpgDbContext context,
    IEnumerable<GameDomainEventListener> listeners,
    IGameClientEventPublisher gameEvents
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
        var clientEvents = new List<GameTurnEvent>();

        var actionResult = await action(input, cancellationToken);
        await Dispatch(actionResult.DomainEvents, clientEvents, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var clientEvent in clientEvents)
        {
            gameEvents.Publish(clientEvent);
        }

        return actionResult.Result;
    }

    private async Task Dispatch(
        IReadOnlyCollection<GameDomainEvent> domainEvents,
        List<GameTurnEvent> clientEvents,
        CancellationToken cancellationToken
    )
    {
        foreach (var domainEvent in domainEvents)
        {
            foreach (var listener in listeners)
            {
                var events = await listener.Handle(domainEvent, cancellationToken);
                clientEvents.AddRange(events);
            }
        }
    }
}
