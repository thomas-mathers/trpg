using TRPG.Data.Models;
using static TRPG.Application.Worlds.Generators.ItemModifierHelpers;

namespace TRPG.Application.Worlds.Generators;

public class ConsumableGenerator
{
    public static readonly IReadOnlyDictionary<ResourceType, string> PotionNamesByResource =
        new Dictionary<ResourceType, string>
        {
            [ResourceType.Hp] = "Health Potion",
            [ResourceType.Mp] = "Mana Potion",
            [ResourceType.Ap] = "Stamina Potion",
        };

    public Consumable Generate(int level, Guid worldId)
    {
        var resources = PotionNamesByResource.Keys.ToArray();
        var resource = resources[Random.Shared.Next(resources.Length)];
        return Generate(resource, level, worldId);
    }

    public Consumable Generate(ResourceType resource, int level, Guid worldId)
    {
        return new Consumable
        {
            WorldId = worldId,
            Level = level,
            Rarity = ItemRarity.Normal,
            Name = PotionNamesByResource[resource],
            Description = "",
            Weight = 1,
            GoldValue = level * 5 + Random.Shared.Next(11),
            Resource = resource,
            RestoreAmount = Roll(level, 20, 100),
            Duration = 0,
        };
    }
}
