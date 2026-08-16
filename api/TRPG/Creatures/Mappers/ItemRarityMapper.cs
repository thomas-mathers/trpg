using ContractItemRarity = TRPG.Contracts.Inventory.Responses.ItemRarity;
using DataItemRarity = TRPG.Domain.Models.ItemRarity;

namespace TRPG.Creatures.Mappers;

internal static class ItemRarityMapper
{
    public static ContractItemRarity ToContract(this DataItemRarity rarity) =>
        rarity switch
        {
            DataItemRarity.Low => ContractItemRarity.Low,
            DataItemRarity.Normal => ContractItemRarity.Normal,
            DataItemRarity.Magic => ContractItemRarity.Magic,
            DataItemRarity.Rare => ContractItemRarity.Rare,
            DataItemRarity.Unique => ContractItemRarity.Unique,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null),
        };
}
