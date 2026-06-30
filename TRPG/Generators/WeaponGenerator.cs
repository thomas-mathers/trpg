using TRPG.Definitions;
using TRPG.Models;
using static TRPG.Generators.ItemModifierHelpers;

namespace TRPG.Generators;

internal class WeaponGenerator(AbilityDefinitions abilityDefinitions) {
    private static readonly string[] Prefixes =
        ["Jagged", "Fine", "Cruel", "Deadly", "Sharp", "Ancient", "Rusted", "Gleaming", "Cursed", "Blessed"];

    private record WeaponTypeData(
        string[] BaseNames,
        int Weight,
        int MinimumDamageLow,
        int MinimumDamageHigh,
        int MaximumDamageLow,
        int MaximumDamageHigh,
        int Range,
        int AttackSpeed
    );

    private static readonly Dictionary<WeaponType, WeaponTypeData> Types = new() {
        [WeaponType.Dagger] = new WeaponTypeData(
            ["Dagger", "Dirk", "Stiletto", "Kris", "Knife"],
            2, 1, 5, 5, 20, 1, 10),
        [WeaponType.Sword] = new WeaponTypeData(
            ["Longsword", "Broadsword", "Falchion", "Saber", "Rapier", "Claymore", "Scimitar", "Shortsword"],
            8, 3, 8, 10, 35, 1, 7),
        [WeaponType.Axe] = new WeaponTypeData(
            ["Hand Axe", "Battle Axe", "War Axe", "Hatchet", "Broad Axe"],
            10, 4, 10, 12, 40, 1, 6),
        [WeaponType.Mace] = new WeaponTypeData(
            ["Mace", "Morning Star", "Flail", "Club", "War Club"],
            9, 3, 8, 10, 35, 1, 6),
        [WeaponType.Hammer] = new WeaponTypeData(
            ["Warhammer", "Maul", "Great Hammer", "Sledgehammer"],
            12, 5, 12, 15, 45, 1, 5),
        [WeaponType.Staff] = new WeaponTypeData(
            ["Oak Staff", "Gnarled Staff", "Battle Staff", "War Staff", "Crystal Staff", "Runed Staff"],
            6, 2, 6, 8, 25, 2, 7),
        [WeaponType.Wand] = new WeaponTypeData(
            ["Wand", "Rod", "Scepter", "Bone Wand", "Grim Wand"],
            2, 1, 4, 4, 15, 4, 9),
        [WeaponType.Bow] = new WeaponTypeData(
            ["Short Bow", "Long Bow", "Composite Bow", "Hunting Bow", "War Bow", "Recurve Bow"],
            5, 2, 6, 8, 30, 15, 6),
        [WeaponType.Crossbow] = new WeaponTypeData(
            ["Light Crossbow", "Heavy Crossbow", "Repeating Crossbow"],
            7, 4, 10, 12, 40, 18, 4),
        [WeaponType.Javelin] = new WeaponTypeData(
            ["Javelin", "Pilum", "War Dart", "Throwing Spear"],
            4, 3, 8, 10, 35, 6, 7)
    };

    private readonly ModifierTemplate[] _modifiers = CreateModifierPool(abilityDefinitions.RandomAttackAbility);

    private static ModifierTemplate[] CreateModifierPool(Func<string> randomAttackAbility) => [
        new(1, ModifierKey.Strength, 100,
            level => new AttributeModifier
                { Attribute = AttributeName.Strength, Type = AmountType.Flat, Amount = Roll(level, 1, 15) }),
        new(1, ModifierKey.Dexterity, 100,
            level => new AttributeModifier
                { Attribute = AttributeName.Dexterity, Type = AmountType.Flat, Amount = Roll(level, 1, 15) }),
        new(5, ModifierKey.IncreasedAttackSpeed, 50,
            level => new CombatSpeedModifier
                { SpeedType = CombatSpeedType.IncreasedAttackSpeed, Amount = Roll(level, 5, 30) }),
        new(5, ModifierKey.FireDamage, 80,
            level => new ElementalDamageModifier
                { DamageType = DamageType.Fire, MinDamage = Roll(level, 1, 20), MaxDamage = Roll(level, 5, 60) }),
        new(5, ModifierKey.IceDamage, 80,
            level => new ElementalDamageModifier
                { DamageType = DamageType.Ice, MinDamage = Roll(level, 1, 20), MaxDamage = Roll(level, 5, 60) }),
        new(5, ModifierKey.LightningDamage, 80,
            level => new ElementalDamageModifier
                { DamageType = DamageType.Lightning, MinDamage = Roll(level, 1, 20), MaxDamage = Roll(level, 5, 60) }),
        new(10, ModifierKey.PoisonDamage, 60,
            level => new ElementalDamageModifier
                { DamageType = DamageType.Poison, MinDamage = Roll(level, 1, 15), MaxDamage = Roll(level, 3, 40) }),
        new(10, ModifierKey.LifeLeech, 40,
            level => new LeechModifier { LeechType = LeechType.Life, Percent = Roll(level, 2, 12) }),
        new(10, ModifierKey.ManaLeech, 30,
            level => new LeechModifier { LeechType = LeechType.Mana, Percent = Roll(level, 2, 12) }),
        new(15, ModifierKey.DeadlyStrike, 20,
            level => new SpecialHitModifier { HitType = SpecialHitType.DeadlyStrike, Chance = Roll(level, 5, 35) }),
        new(15, ModifierKey.OpenWounds, 20,
            level => new SpecialHitModifier { HitType = SpecialHitType.OpenWounds, Chance = Roll(level, 5, 35) }),
        new(20, ModifierKey.CrushingBlow, 10,
            level => new SpecialHitModifier { HitType = SpecialHitType.CrushingBlow, Chance = Roll(level, 5, 25) }),
        new(25, ModifierKey.ProcOnStriking, 5,
            level => new ProcModifier
                { AbilityName = randomAttackAbility(), Chance = Roll(level, 5, 20), Trigger = ProcTrigger.OnStriking })
    ];

    public WeaponItem Generate(WeaponType type, int level, Guid worldId) {
        var data = Types.GetValueOrDefault(type, new WeaponTypeData([type.ToString()], 6, 2, 6, 8, 25, 1, 7));
        var baseName = data.BaseNames[Random.Shared.Next(data.BaseNames.Length)];
        var prefix = Prefixes[Random.Shared.Next(Prefixes.Length)];
        var eligible = _modifiers.Where(t => t.MinItemLevel <= level).ToList();
        var modifiers = PickModifiers(eligible, ModifierCount(level), level);
        var durabilityMax = 50 + level * 5;

        return new WeaponItem {
            WorldId = worldId,
            Level = level,
            Rarity = ItemRarity.Normal,
            Type = type,
            Name = $"{prefix} {baseName}",
            Description = "",
            Weight = data.Weight,
            GoldValue = level * 10 + modifiers.Count * 50 + Random.Shared.Next(level * 5 + 1),
            Modifiers = modifiers,
            MinDamage = Roll(level, data.MinimumDamageLow, data.MinimumDamageHigh),
            MaxDamage = Roll(level, data.MaximumDamageLow, data.MaximumDamageHigh),
            Range = data.Range,
            AttackSpeed = data.AttackSpeed,
            DurabilityMax = durabilityMax,
            DurabilityCurrent = durabilityMax
        };
    }
}
