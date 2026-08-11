using System.Transactions;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Inventory.Commands;

internal class TransferPlayerInventoryCommand
{
    public required ItemOwnerReference To { get; init; }
    public required IReadOnlyList<ItemSelection> Items { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class TransferPlayerInventoryCommandHandler(
    TrpgDbContext context,
    InventoryItemTransfer itemTransfer,
    AddGoldCommandHandler addGold
)
{
    public async Task Handle(
        TransferPlayerInventoryCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var goldTransfers =
            command.Items.Count == 0
                ? []
                : await itemTransfer.Transfer(
                    new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                    command.To,
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

        transaction.Complete();
    }
}
