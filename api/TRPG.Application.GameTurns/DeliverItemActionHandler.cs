using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class DeliverItemActionHandler(
    GameActionRunner actionRunner,
    IQueryHandler<GetDeliverableItemForRecipientQuery, DeliverableItemResult?> getDeliverableItem,
    ICommandHandler<SetItemsCanTradeCommand> setItemsCanTrade,
    ICommandHandler<TransferPlayerInventoryCommand> transferPlayerInventory
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        Guid recipientId,
        CancellationToken cancellationToken = default
    ) => actionRunner.Run(session, ct => Resolve(session, recipientId, ct), cancellationToken);

    private async Task<ActionOutcome> Resolve(
        GameTurnSession session,
        Guid recipientId,
        CancellationToken cancellationToken
    )
    {
        var deliverable = await getDeliverableItem.Handle(
            new GetDeliverableItemForRecipientQuery
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                RecipientId = recipientId,
            },
            cancellationToken
        );
        if (deliverable == null)
        {
            return ActionOutcome.Failed(ActionFailure.NothingToDeliver);
        }

        // The item was locked from trading when the quest was accepted, to stop it being sold or
        // given away by mistake — unlock it here so this deliberate, code-driven handoff can go
        // through, same as CompleteQuestCommand does immediately before its own transfer.
        await setItemsCanTrade.Handle(
            new SetItemsCanTradeCommand { ItemIds = [deliverable.ItemId], CanTrade = true },
            cancellationToken
        );

        await transferPlayerInventory.Handle(
            new TransferPlayerInventoryCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                To = new ItemOwnerReference(recipientId, OwnerType.Creature),
                Items = [new ItemSelection(deliverable.ItemId, 1)],
            },
            cancellationToken
        );

        return ActionOutcome.Success;
    }
}
