using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Commands;

public class ReceivePlayerInventoryCommand
{
    public required ItemOwnerReference From { get; init; }
    public required IReadOnlyList<ItemSelection> Items { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class ReceivePlayerInventoryCommandHandler(
    ICommandHandler<
        TransferInventoryItemsCommand,
        IReadOnlyCollection<InventoryItemTransferResult>
    > transferInventoryItems,
    IDomainEventPublisher<ItemAcquiredEvent> domainEvents
) : ICommandHandler<ReceivePlayerInventoryCommand>
{
    public async Task Handle(
        ReceivePlayerInventoryCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Items.Count == 0)
        {
            return;
        }

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var playerOwner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature);

        await transferInventoryItems.Handle(
            new TransferInventoryItemsCommand
            {
                From = command.From,
                To = playerOwner,
                Items = command.Items,
            },
            cancellationToken
        );

        foreach (var item in command.Items)
        {
            await domainEvents.Publish(
                new ItemAcquiredEvent(command.PlayerId, command.WorldId, item.ItemId),
                cancellationToken
            );
        }

        transaction.Complete();
    }
}
