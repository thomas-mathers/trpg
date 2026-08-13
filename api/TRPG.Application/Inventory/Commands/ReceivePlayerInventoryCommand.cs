using System.Transactions;
using TRPG.Application.Quests;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Commands;

internal class ReceivePlayerInventoryCommand
{
    public required ItemOwnerReference From { get; init; }
    public required IReadOnlyList<ItemSelection> Items { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class ReceivePlayerInventoryCommandHandler(
    TrpgDbContext context,
    InventoryItemTransfer itemTransfer,
    ItemAcquiredQuestEventHandler itemAcquiredQuestEvents
)
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

        await itemTransfer.Transfer(command.From, playerOwner, command.Items, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        foreach (var item in command.Items)
        {
            await itemAcquiredQuestEvents.Handle(
                new ItemAcquiredQuestEvent(command.PlayerId, command.WorldId, item.ItemId),
                cancellationToken
            );
        }

        transaction.Complete();
    }
}
