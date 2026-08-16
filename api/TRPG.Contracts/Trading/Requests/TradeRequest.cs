using TRPG.Domain.Models;

namespace TRPG.Contracts.Trading.Requests;

public record TradeRequest(
    IReadOnlyList<ItemSelection> PlayerOffer,
    IReadOnlyList<ItemSelection> ShopOffer
);
