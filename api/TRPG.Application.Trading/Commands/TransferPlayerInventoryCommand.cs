using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Inventory;
using TRPG.Domain.Models;

namespace TRPG.Application.Trading.Commands;

public class TransferPlayerInventoryCommand
{
    public required ItemOwnerReference To { get; init; }
    public required IReadOnlyList<ItemSelection> Items { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class TransferPlayerInventoryCommandHandler(
    ICommandHandler<
        TransferInventoryItemsCommand,
        IReadOnlyCollection<InventoryItemTransferResult>
    > transferInventoryItems,
    QuestItemGuard questItemGuard
) : ICommandHandler<TransferPlayerInventoryCommand>
{
    public async Task Handle(
        TransferPlayerInventoryCommand command,
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

        await questItemGuard.EnsureNotActiveQuestItems(
            command.PlayerId,
            command.Items.Select(item => item.ItemId).ToArray(),
            cancellationToken
        );

        await transferInventoryItems.Handle(
            new TransferInventoryItemsCommand
            {
                From = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                To = command.To,
                Items = command.Items,
            },
            cancellationToken
        );

        transaction.Complete();
    }
}
