using TRPG.Models;

namespace TRPG.Generators;

internal enum ModifierKey {
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
    ProcWhenStruck
}

internal record ModifierTemplate(int MinItemLevel, ModifierKey UniqueKey, int Weight, Func<int, ItemModifier> Build);

internal static class ItemModifierHelpers {
    internal static List<ItemModifier> PickModifiers(List<ModifierTemplate> pool, int count, int itemLevel) {
        var remaining = pool.ToList();
        var result = new List<ItemModifier>();
        for (var i = 0; i < count && remaining.Count > 0; i++) {
            var template = WeightedRandom(remaining);
            result.Add(template.Build(itemLevel));
            remaining.RemoveAll(t => t.UniqueKey == template.UniqueKey);
        }
        return result;
    }

    internal static int ModifierCount(int itemLevel) =>
        itemLevel switch {
            <= 5  => Random.Shared.Next(0, 2),
            <= 15 => Random.Shared.Next(1, 3),
            <= 30 => Random.Shared.Next(2, 4),
            <= 60 => Random.Shared.Next(2, 5),
            _     => Random.Shared.Next(3, 6)
        };

    internal static int Roll(int itemLevel, int minimum, int maximum) {
        var range = maximum - minimum;
        var progress = Math.Clamp(itemLevel / 100f, 0f, 1f);
        var jitter = Random.Shared.NextSingle() * (range * 0.2f);
        return Math.Max(1, (int) MathF.Round(minimum + range * progress + jitter));
    }

    private static ModifierTemplate WeightedRandom(List<ModifierTemplate> pool) {
        var total = pool.Sum(t => t.Weight);
        var roll = Random.Shared.Next(total);
        var cumulative = 0;
        foreach (var t in pool) {
            cumulative += t.Weight;
            if (roll < cumulative) return t;
        }
        return pool[^1];
    }
}
