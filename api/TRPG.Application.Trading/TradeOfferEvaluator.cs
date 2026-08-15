using TRPG.Application.Inventory;

namespace TRPG.Application.Trading;

internal enum TradeOutcome
{
    Accepted,
    Rejected,
}

internal record ValidatedTradeOffer(
    ItemOwnerReference ShopOwner,
    int PlayerOfferValue,
    int ShopOfferValue
);

internal class TradeOfferEvaluator
{
    public TradeOutcome Evaluate(ValidatedTradeOffer offer) =>
        offer.PlayerOfferValue < offer.ShopOfferValue
            ? TradeOutcome.Rejected
            : TradeOutcome.Accepted;
}
