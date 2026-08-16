using TRPG.Domain.Models;

namespace TRPG.Inventory.Requests;

public record TradeRequest(
    IReadOnlyList<ItemSelection> PlayerOffer,
    IReadOnlyList<ItemSelection> ShopOffer
);
