using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public class AmmoGenerator
{
    private static readonly Dictionary<AmmoType, string[]> BaseNames = new()
    {
        [AmmoType.Arrow] = ["Arrows", "Bodkin Arrows", "Broadhead Arrows", "Fire Arrows"],
        [AmmoType.Bolt] = ["Bolts", "Broadhead Bolts", "Steel Bolts"],
    };

    public Ammunition Generate(AmmoType type, Guid worldId)
    {
        var names = BaseNames.GetValueOrDefault(type, [type.ToString()]);
        var baseName = names[Random.Shared.Next(names.Length)];
        return new Ammunition
        {
            WorldId = worldId,
            Level = 1,
            Rarity = ItemRarity.Normal,
            Type = type,
            Name = baseName,
            Description = "",
            Weight = 2,
            GoldValue = 5,
        };
    }
}
