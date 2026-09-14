using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamDeliverItemTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetDeliverableItemForRecipientQuery, DeliverableItemResult?> getDeliverableItem,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<SetItemsCanTradeCommand> setItemsCanTrade,
    ICommandHandler<TransferPlayerInventoryCommand> transferPlayerInventory
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        Guid recipientId,
        CancellationToken cancellationToken = default
    ) =>
        streamer.StreamTurn(
            session,
            ct => ResolveTurn(session, recipientId, ct),
            cancellationToken
        );

    private async Task<GameTurnPrompt> ResolveTurn(
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
            return new GameTurnPrompt.Reply("You have nothing to deliver to them.");
        }

        var recipient = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = recipientId },
            cancellationToken
        );

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

        return new GameTurnPrompt.Narrate(
            $"The player just handed {recipient?.Name ?? "them"} the item meant for them as "
                + $"part of the quest \"{deliverable.QuestName}\". Narrate a brief in-character "
                + $"reaction from {recipient?.Name ?? "them"} to receiving it — in one or two "
                + "sentences. Do not mention game mechanics or rewards."
        );
    }
}
