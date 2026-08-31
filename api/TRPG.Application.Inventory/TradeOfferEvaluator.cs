namespace TRPG.Application.Inventory;

public enum TradeOutcome
{
    Accepted,
    Rejected,
    Refused,
}

internal record ValidatedTradeOffer(
    ItemOwnerReference ShopOwner,
    Guid? AssignedCreatureId,
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
