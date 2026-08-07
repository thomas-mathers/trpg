using TRPG.Application.Abilities;
using TRPG.Data.Models;
using static TRPG.Application.Worlds.Generators.ItemModifierHelpers;

namespace TRPG.Application.Worlds.Generators;

public class WeaponGenerator
{
    private static readonly Dictionary<WeaponType, WeaponTypeData> Types = new()
    {
        [WeaponType.Dagger] = new WeaponTypeData(
            ["Dagger", "Dirk", "Stiletto", "Kris", "Knife"],
            2,
            2,
            7,
            8,
            27,
            1,
            2
        ),
        [WeaponType.Sword] = new WeaponTypeData(
            [
                "Longsword",
                "Broadsword",
                "Falchion",
                "Saber",
                "Rapier",
                "Claymore",
                "Scimitar",
                "Shortsword",
            ],
            8,
            3,
            8,
            10,
            35,
            1,
            1
        ),
        [WeaponType.Axe] = new WeaponTypeData(
            ["Hand Axe", "Battle Axe", "War Axe", "Hatchet", "Broad Axe"],
            10,
            4,
            10,
            12,
            40,
            1,
            1
        ),
        [WeaponType.Mace] = new WeaponTypeData(
            ["Mace", "Morning Star", "Flail", "Club", "War Club"],
            9,
            3,
            8,
            10,
            35,
            1,
            1
        ),
        [WeaponType.Hammer] = new WeaponTypeData(
            ["Warhammer", "Maul", "Sledgehammer"],
            12,
            5,
            12,
            15,
            45,
            1,
            1
        ),
        [WeaponType.Staff] = new WeaponTypeData(
            [
                "Oak Staff",
                "Gnarled Staff",
                "Battle Staff",
                "War Staff",
                "Crystal Staff",
                "Runed Staff",
            ],
            6,
            2,
            6,
            8,
            25,
            2,
            1
        ),
        [WeaponType.Wand] = new WeaponTypeData(
            ["Wand", "Rod", "Scepter", "Bone Wand", "Grim Wand"],
            2,
            1,
            4,
            4,
            15,
            4,
            1
        ),
        [WeaponType.Bow] = new WeaponTypeData(
            ["Short Bow", "Long Bow", "Composite Bow", "Hunting Bow", "War Bow", "Recurve Bow"],
            5,
            2,
            6,
            8,
            30,
            15,
            1,
            IsTwoHanded: true
        ),
        [WeaponType.Crossbow] = new WeaponTypeData(
            ["Light Crossbow", "Heavy Crossbow", "Repeating Crossbow"],
            7,
            4,
            10,
            12,
            40,
            18,
            1,
            IsTwoHanded: true
        ),
        [WeaponType.Javelin] = new WeaponTypeData(
            ["Javelin", "Pilum", "War Dart", "Throwing Spear"],
            4,
            3,
            8,
            10,
            35,
            6,
            1
        ),
        [WeaponType.GreatSword] = new WeaponTypeData(
            ["Greatsword", "Zweihander", "Claymore", "Executioner's Blade"],
            14,
            5,
            12,
            15,
            50,
            1,
            1,
            IsTwoHanded: true
        ),
        [WeaponType.GreatAxe] = new WeaponTypeData(
            ["Great Axe", "Battleaxe", "War Cleaver", "Reaper's Axe"],
            16,
            6,
            14,
            18,
            55,
            1,
            1,
            IsTwoHanded: true
        ),
        [WeaponType.GreatHammer] = new WeaponTypeData(
            ["Great Hammer", "Warmaul", "Skullcrusher", "Earth Breaker"],
            18,
            7,
            16,
            20,
            60,
            1,
            1,
            IsTwoHanded: true
        ),
    };

    private readonly ModifierTemplate[] _modifiers =
    [
        new(
            1,
            ModifierKey.Strength,
            100,
            level => new AttributeModifier
            {
                Attribute = AttributeName.Strength,
                AmountType = AmountType.Flat,
                Amount = Roll(level, 1, 15),
            }
        ),
        new(
            1,
            ModifierKey.Dexterity,
            100,
            level => new AttributeModifier
            {
                Attribute = AttributeName.Dexterity,
                AmountType = AmountType.Flat,
                Amount = Roll(level, 1, 15),
            }
        ),
        new(
            5,
            ModifierKey.IncreasedAttackSpeed,
            50,
            level => new CombatSpeedModifier
            {
                SpeedType = CombatSpeedType.IncreasedAttackSpeed,
                Amount = Roll(level, 5, 30),
            }
        ),
        new(
            5,
            ModifierKey.FireDamage,
            80,
            level => new ElementalDamageModifier
            {
                DamageType = DamageType.Fire,
                MinDamage = Roll(level, 1, 20),
                MaxDamage = Roll(level, 5, 60),
            }
        ),
        new(
            5,
            ModifierKey.IceDamage,
            80,
            level => new ElementalDamageModifier
            {
                DamageType = DamageType.Ice,
                MinDamage = Roll(level, 1, 20),
                MaxDamage = Roll(level, 5, 60),
            }
        ),
        new(
            5,
            ModifierKey.LightningDamage,
            80,
            level => new ElementalDamageModifier
            {
                DamageType = DamageType.Lightning,
                MinDamage = Roll(level, 1, 20),
                MaxDamage = Roll(level, 5, 60),
            }
        ),
        new(
            10,
            ModifierKey.PoisonDamage,
            60,
            level => new ElementalDamageModifier
            {
                DamageType = DamageType.Poison,
                MinDamage = Roll(level, 1, 15),
                MaxDamage = Roll(level, 3, 40),
            }
        ),
        new(
            10,
            ModifierKey.LifeLeech,
            40,
            level => new LeechModifier { LeechType = LeechType.Life, Percent = Roll(level, 2, 12) }
        ),
        new(
            10,
            ModifierKey.ManaLeech,
            30,
            level => new LeechModifier { LeechType = LeechType.Mana, Percent = Roll(level, 2, 12) }
        ),
        new(
            15,
            ModifierKey.DeadlyStrike,
            20,
            level => new SpecialHitModifier
            {
                HitType = SpecialHitType.DeadlyStrike,
                Chance = Roll(level, 5, 35) / 100f,
            }
        ),
        new(
            15,
            ModifierKey.OpenWounds,
            20,
            level => new SpecialHitModifier
            {
                HitType = SpecialHitType.OpenWounds,
                Chance = Roll(level, 5, 35) / 100f,
            }
        ),
        new(
            20,
            ModifierKey.CrushingBlow,
            10,
            level => new SpecialHitModifier
            {
                HitType = SpecialHitType.CrushingBlow,
                Chance = Roll(level, 5, 25) / 100f,
            }
        ),
        new(
            25,
            ModifierKey.ProcOnStriking,
            5,
            level => new ProcModifier
            {
                AbilityName = AbilityCatalog.Abilities.RandomAttackAbility(),
                Chance = Roll(level, 5, 20) / 100f,
                Trigger = ProcTrigger.OnStriking,
            }
        ),
    ];

    public Weapon Generate(WeaponType type, int level, Guid worldId)
    {
        var data = Types.GetValueOrDefault(
            type,
            new WeaponTypeData([type.ToString()], 6, 2, 6, 8, 25, 1, 1)
        );
        var baseName = data.BaseNames[Random.Shared.Next(data.BaseNames.Length)];
        var eligible = _modifiers.Where(t => t.MinItemLevel <= level).ToList();
        var chosen = PickModifierTemplates(eligible, ModifierCount(level), level);
        var modifiers = chosen.Select(t => t.Build(level)).ToList();
        var durabilityMax = 50 + level * 5;

        return new Weapon
        {
            WorldId = worldId,
            Level = level,
            Rarity = ItemRarity.Normal,
            Type = type,
            Name = BuildName(baseName, chosen),
            Description = "",
            Weight = data.Weight,
            GoldValue = level * 10 + modifiers.Count * 50 + Random.Shared.Next(level * 5 + 1),
            Modifiers = modifiers,
            MinDamage = Roll(level, data.MinimumDamageLow, data.MinimumDamageHigh),
            MaxDamage = Roll(level, data.MaximumDamageLow, data.MaximumDamageHigh),
            Range = data.Range,
            AttacksPerTurn = data.AttacksPerTurn,
            IsTwoHanded = data.IsTwoHanded,
            DurabilityMax = durabilityMax,
            DurabilityCurrent = durabilityMax,
        };
    }

    private record WeaponTypeData(
        string[] BaseNames,
        int Weight,
        int MinimumDamageLow,
        int MinimumDamageHigh,
        int MaximumDamageLow,
        int MaximumDamageHigh,
        int Range,
        int AttacksPerTurn,
        bool IsTwoHanded = false
    );
}
