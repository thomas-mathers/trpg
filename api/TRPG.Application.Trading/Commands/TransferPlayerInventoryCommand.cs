using System.Transactions;
using TRPG.Application.Common.Handling;
using TRPG.Application.Inventory;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Trading.Commands;

public class TransferPlayerInventoryCommand
{
    public required ItemOwnerReference To { get; init; }
    public required IReadOnlyList<ItemSelection> Items { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class TransferPlayerInventoryCommandHandler(
    TrpgDbContext context,
    InventoryItemTransfer itemTransfer,
    IQueryHandler<GetActiveQuestItemIdsQuery, IReadOnlyCollection<Guid>> getActiveQuestItemIds
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

        await EnsureQuestItemsRemainWithPlayer(command, cancellationToken);

        await itemTransfer.Transfer(
            new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
            command.To,
            command.Items,
            cancellationToken
        );

        await context.SaveChangesAsync(cancellationToken);

        transaction.Complete();
    }

    private async Task EnsureQuestItemsRemainWithPlayer(
        TransferPlayerInventoryCommand command,
        CancellationToken cancellationToken
    )
    {
        var questItemIds = await getActiveQuestItemIds.Handle(
            new GetActiveQuestItemIdsQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        var questItemId = command
            .Items.Select(item => item.ItemId)
            .FirstOrDefault(questItemIds.Contains);

        if (questItemId != Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Item {questItemId} is required for an active quest and cannot be transferred."
            );
        }
    }
}
