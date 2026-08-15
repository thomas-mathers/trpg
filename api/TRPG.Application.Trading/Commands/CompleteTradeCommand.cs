using System.Transactions;
using TRPG.Contracts.Inventory.Requests;

namespace TRPG.Application.Trading.Commands;

internal class CompleteTradeCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid WorkstationId { get; init; }
    public required IReadOnlyList<ItemSelection> PlayerOffer { get; init; }
    public required IReadOnlyList<ItemSelection> ShopOffer { get; init; }
}

internal class CompleteTradeCommandHandler(
    TradeOfferValidator validator,
    TradeOfferEvaluator evaluator,
    ReceivePlayerInventoryCommandHandler receiveInventory,
    TransferPlayerInventoryCommandHandler transferInventory
)
{
    public async Task Handle(
        CompleteTradeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var validation = await validator.Validate(
            command.PlayerId,
            command.WorkstationId,
            command.PlayerOffer,
            command.ShopOffer,
            cancellationToken
        );

        if (evaluator.Evaluate(validation) == TradeOutcome.Rejected)
            throw new InvalidOperationException("Trade offer is no longer accepted.");

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await transferInventory.Handle(
            new TransferPlayerInventoryCommand
            {
                To = validation.ShopOwner,
                Items = command.PlayerOffer,
                PlayerId = command.PlayerId,
            },
            cancellationToken
        );

        await receiveInventory.Handle(
            new ReceivePlayerInventoryCommand
            {
                From = validation.ShopOwner,
                Items = command.ShopOffer,
                PlayerId = command.PlayerId,
                WorldId = command.WorldId,
            },
            cancellationToken
        );

        transaction.Complete();
    }
}
