using TRPG.Contracts.Inventory.Requests;

namespace TRPG.Application.Trading;

internal class ProposeTradeCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorkstationId { get; init; }
    public required IReadOnlyList<ItemSelection> PlayerOffer { get; init; }
    public required IReadOnlyList<ItemSelection> ShopOffer { get; init; }
}

internal class ProposeTradeCommandHandler(
    TradeOfferValidator validator,
    TradeOfferEvaluator evaluator
)
{
    public async Task<TradeOutcome> Handle(
        ProposeTradeCommand command,
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

        return evaluator.Evaluate(validation);
    }
}
