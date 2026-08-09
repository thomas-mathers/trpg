using TRPG.Contracts.Inventory.Requests;

namespace TRPG.Contracts.Trading.Requests;

public record TradeRequest(
    IReadOnlyList<ItemSelection> PlayerOffer,
    IReadOnlyList<ItemSelection> ShopOffer
);
