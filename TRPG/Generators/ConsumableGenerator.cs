using TRPG.Models;
using static TRPG.Generators.ItemModifierHelpers;

namespace TRPG.Generators;

internal class ConsumableGenerator {
    private static readonly string[] BaseNames =
        ["Health Potion", "Mana Potion", "Antidote", "Elixir", "Tonic"];

    public ConsumableItem Generate(int level, Guid worldId) {
        var baseName = BaseNames[Random.Shared.Next(BaseNames.Length)];
        return new ConsumableItem {
            WorldId = worldId,
            Level = level,
            Rarity = ItemRarity.Normal,
            Name = baseName,
            Description = "",
            Weight = 1,
            GoldValue = level * 5 + Random.Shared.Next(11),
            Attribute = AttributeName.CurrentHp,
            Amount = Roll(level, 20, 100),
            Duration = 0
        };
    }
}
