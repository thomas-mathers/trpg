using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Generators;

internal record LevelScaling(int MinLevel, int MaxLevel, float Rate);

internal static class CreatureLevelScaling
{
    private static readonly Dictionary<CreatureType, LevelScaling> ByCreatureType = new()
    {
        [CreatureType.Beast] = new LevelScaling(MinLevel: 1, MaxLevel: 10, Rate: 0.3f),
        [CreatureType.Elemental] = new LevelScaling(MinLevel: 1, MaxLevel: 10, Rate: 0.3f),
        [CreatureType.Goblin] = new LevelScaling(MinLevel: 1, MaxLevel: 15, Rate: 0.4f),
        [CreatureType.Construct] = new LevelScaling(MinLevel: 1, MaxLevel: 20, Rate: 0.5f),
        [CreatureType.Wraith] = new LevelScaling(MinLevel: 2, MaxLevel: 25, Rate: 0.5f),
        [CreatureType.Undead] = new LevelScaling(MinLevel: 2, MaxLevel: 25, Rate: 0.6f),
        [CreatureType.Giant] = new LevelScaling(MinLevel: 3, MaxLevel: 30, Rate: 0.6f),
        [CreatureType.Demon] = new LevelScaling(MinLevel: 5, MaxLevel: 40, Rate: 0.8f),
        [CreatureType.Dragon] = new LevelScaling(MinLevel: 10, MaxLevel: 60, Rate: 1.0f),
    };

    internal static int SpawnLevel(CreatureType creatureType, int playerLevel)
    {
        var scaling = ByCreatureType[creatureType];
        var scaledLevel = (int)MathF.Round(playerLevel * scaling.Rate);
        return Math.Clamp(scaledLevel, scaling.MinLevel, scaling.MaxLevel);
    }
}
