using System.Transactions;
using TRPG.Application.Common.Events;
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
    AddGoldCommandHandler addGold,
    QuestEventHandler questEvents
)
{
    public async Task Handle(
        ReceivePlayerInventoryCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var playerOwner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature);
        var goldTransfers =
            command.Items.Count == 0
                ? []
                : await itemTransfer.Transfer(
                    command.From,
                    playerOwner,
                    command.Items,
                    cancellationToken
                );

        foreach (var goldTransfer in goldTransfers)
        {
            await addGold.Handle(
                new AddGoldCommand
                {
                    Owner = goldTransfer.To,
                    WorldId = goldTransfer.WorldId,
                    Amount = goldTransfer.Amount,
                },
                cancellationToken
            );
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var item in command.Items)
        {
            await questEvents.Handle(
                new ItemAcquiredEvent(command.PlayerId, command.WorldId, item.ItemId),
                cancellationToken
            );
        }

        transaction.Complete();
    }
}
