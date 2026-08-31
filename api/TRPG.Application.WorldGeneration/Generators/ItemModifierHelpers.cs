using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal enum ModifierKey
{
    Strength,
    Dexterity,
    Intelligence,
    Endurance,
    MaxHp,
    MaxAp,
    Defense,
    FireResistance,
    IceResistance,
    LightningResistance,
    PoisonResistance,
    MagicResistance,
    IncreasedAttackSpeed,
    FasterHitRecovery,
    FasterCastRate,
    FireDamage,
    IceDamage,
    LightningDamage,
    PoisonDamage,
    LifeLeech,
    ManaLeech,
    DeadlyStrike,
    OpenWounds,
    CrushingBlow,
    SkillBonus,
    ProcOnStriking,
    ProcWhenStruck,
}

internal record ModifierTemplate(
    int MinItemLevel,
    ModifierKey UniqueKey,
    int Weight,
    Func<int, ItemModifier> Build
);

internal static class ItemModifierHelpers
{
    private static readonly Dictionary<ModifierKey, ModifierNameData> Names = new()
    {
        [ModifierKey.FireDamage] = new ModifierNameData(["Fiery", "Burning", "Blazing"], []),
        [ModifierKey.IceDamage] = new ModifierNameData(["Glacial", "Frozen", "Icy"], []),
        [ModifierKey.LightningDamage] = new ModifierNameData(
            ["Static", "Shocking", "Thundering"],
            []
        ),
        [ModifierKey.PoisonDamage] = new ModifierNameData(["Venomous", "Toxic", "Envenomed"], []),
        [ModifierKey.LifeLeech] = new ModifierNameData(["Vampiric", "Draining"], []),
        [ModifierKey.ManaLeech] = new ModifierNameData(["Mystical"], []),
        [ModifierKey.DeadlyStrike] = new ModifierNameData(["Deadly", "Lethal"], []),
        [ModifierKey.OpenWounds] = new ModifierNameData(["Wounding", "Serrated"], []),
        [ModifierKey.CrushingBlow] = new ModifierNameData(["Crushing", "Shattering"], []),
        [ModifierKey.ProcOnStriking] = new ModifierNameData(["Runic"], []),
        [ModifierKey.ProcWhenStruck] = new ModifierNameData(["Thorned"], []),
        [ModifierKey.FireResistance] = new ModifierNameData(["Ruby", "Garnet", "Crimson"], []),
        [ModifierKey.IceResistance] = new ModifierNameData(["Sapphire", "Cobalt", "Azure"], []),
        [ModifierKey.LightningResistance] = new ModifierNameData(["Topaz", "Amber", "Gold"], []),
        [ModifierKey.PoisonResistance] = new ModifierNameData(["Emerald", "Jade", "Viridian"], []),
        [ModifierKey.MagicResistance] = new ModifierNameData(["Amethyst", "Arcane"], []),
        [ModifierKey.Strength] = new ModifierNameData([], ["Strength", "the Titan", "Might"]),
        [ModifierKey.Dexterity] = new ModifierNameData([], ["Dexterity", "the Fox", "Skill"]),
        [ModifierKey.Intelligence] = new ModifierNameData(
            [],
            ["Intelligence", "the Sage", "Wisdom"]
        ),
        [ModifierKey.Endurance] = new ModifierNameData([], ["Endurance", "the Bear"]),
        [ModifierKey.MaxHp] = new ModifierNameData([], ["Life", "the Mammoth", "Vitality"]),
        [ModifierKey.MaxAp] = new ModifierNameData([], ["Energy", "Stamina"]),
        [ModifierKey.Defense] = new ModifierNameData([], ["Protection", "Defense", "Warding"]),
        [ModifierKey.IncreasedAttackSpeed] = new ModifierNameData([], ["Quickness", "Alacrity"]),
        [ModifierKey.FasterCastRate] = new ModifierNameData([], ["the Adept", "Swiftness"]),
        [ModifierKey.FasterHitRecovery] = new ModifierNameData([], ["Balance", "Steadiness"]),
        [ModifierKey.SkillBonus] = new ModifierNameData([], ["Mastery", "Skill"]),
    };

    internal static IReadOnlyList<ModifierTemplate> PickModifierTemplates(
        IReadOnlyList<ModifierTemplate> pool,
        int count,
        int itemLevel
    )
    {
        var remaining = pool.ToList();
        var result = new List<ModifierTemplate>();
        for (var i = 0; i < count && remaining.Count > 0; i++)
        {
            var template = WeightedRandom(remaining);
            result.Add(template);
            remaining.RemoveAll(t => t.UniqueKey == template.UniqueKey);
        }

        return result.ToArray();
    }

    internal static string BuildName(string baseName, IReadOnlyList<ModifierTemplate> chosen)
    {
        var withPrefixes = chosen
            .Where(t => Names.TryGetValue(t.UniqueKey, out var n) && n.Prefixes.Length > 0)
            .ToList();
        var withSuffixes = chosen
            .Where(t => Names.TryGetValue(t.UniqueKey, out var n) && n.Suffixes.Length > 0)
            .ToList();

        string? prefix = null;
        string? suffix = null;

        if (withPrefixes.Count > 0)
        {
            var names = Names[withPrefixes[Random.Shared.Next(withPrefixes.Count)].UniqueKey];
            prefix = names.Prefixes[Random.Shared.Next(names.Prefixes.Length)];
        }

        if (withSuffixes.Count > 0)
        {
            var names = Names[withSuffixes[Random.Shared.Next(withSuffixes.Count)].UniqueKey];
            suffix = names.Suffixes[Random.Shared.Next(names.Suffixes.Length)];
        }

        return (prefix, suffix) switch
        {
            (not null, not null) => $"{prefix} {baseName} of {suffix}",
            (not null, null) => $"{prefix} {baseName}",
            (null, not null) => $"{baseName} of {suffix}",
            _ => baseName,
        };
    }

    internal static int ModifierCount(int itemLevel)
    {
        return itemLevel switch
        {
            <= 5 => Random.Shared.Next(0, 2),
            <= 15 => Random.Shared.Next(1, 3),
            <= 30 => Random.Shared.Next(2, 4),
            <= 60 => Random.Shared.Next(2, 5),
            _ => Random.Shared.Next(3, 6),
        };
    }

    internal static int Roll(int itemLevel, int minimum, int maximum)
    {
        var range = maximum - minimum;
        var progress = Math.Clamp(itemLevel / 100f, 0f, 1f);
        var jitter = Random.Shared.NextSingle() * (range * 0.2f);
        return Math.Max(1, (int)MathF.Round(minimum + range * progress + jitter));
    }

    private static ModifierTemplate WeightedRandom(List<ModifierTemplate> pool)
    {
        var total = pool.Sum(t => t.Weight);
        var roll = Random.Shared.Next(total);
        var cumulative = 0;
        foreach (var t in pool)
        {
            cumulative += t.Weight;
            if (roll < cumulative)
            {
                return t;
            }
        }

        return pool[^1];
    }

    private record ModifierNameData(string[] Prefixes, string[] Suffixes);
}
