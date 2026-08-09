using TRPG.Application.Abilities;
using TRPG.Data.Models;
using static TRPG.Application.Worlds.Generators.ItemModifierHelpers;

namespace TRPG.Application.Worlds.Generators;

public class ArmorGenerator
{
    private static readonly string[] ShieldBaseNames =
    [
        "Buckler",
        "Small Shield",
        "Large Shield",
        "Tower Shield",
        "Kite Shield",
        "Round Shield",
    ];

    private static readonly Dictionary<ArmorType, Dictionary<ArmorClass, ArmorClassData>> Types =
        new()
        {
            [ArmorType.Helm] = new Dictionary<ArmorClass, ArmorClassData>
            {
                [ArmorClass.Cloth] = new(["Hood", "Cowl", "Skullcap"], 1, 0, 2),
                [ArmorClass.Leather] = new(["Cap", "Leather Cap", "Hunter's Hood"], 2, 2, 8),
                [ArmorClass.Mail] = new(["Coif", "Chain Coif", "Mail Coif"], 4, 5, 15),
                [ArmorClass.Plate] = new(
                    ["Helm", "Great Helm", "War Helm", "Visor", "Full Helm"],
                    6,
                    10,
                    25
                ),
            },
            [ArmorType.Chest] = new Dictionary<ArmorClass, ArmorClassData>
            {
                [ArmorClass.Cloth] = new(["Robe", "Tunic", "Vestments", "Garb"], 2, 0, 5),
                [ArmorClass.Leather] = new(
                    ["Leather Armor", "Studded Leather", "Brigandine"],
                    5,
                    5,
                    20
                ),
                [ArmorClass.Mail] = new(["Ring Mail", "Chain Mail", "Scale Mail"], 10, 15, 40),
                [ArmorClass.Plate] = new(
                    ["Plate Mail", "Full Plate", "Hauberk", "Breastplate"],
                    15,
                    30,
                    70
                ),
            },
            [ArmorType.Boots] = new Dictionary<ArmorClass, ArmorClassData>
            {
                [ArmorClass.Cloth] = new(["Shoes", "Sandals", "Light Shoes"], 1, 0, 1),
                [ArmorClass.Leather] = new(["Boots", "Leather Boots", "Light Boots"], 2, 1, 5),
                [ArmorClass.Mail] = new(["Greaves", "Chain Boots"], 4, 3, 10),
                [ArmorClass.Plate] = new(["Sabatons", "War Boots", "Heavy Boots"], 6, 6, 20),
            },
            [ArmorType.Gloves] = new Dictionary<ArmorClass, ArmorClassData>
            {
                [ArmorClass.Cloth] = new(["Gloves", "Light Gloves"], 0, 0, 1),
                [ArmorClass.Leather] = new(["Leather Gloves", "Bracers"], 1, 1, 4),
                [ArmorClass.Mail] = new(["Chain Gloves", "Mail Gauntlets"], 2, 2, 8),
                [ArmorClass.Plate] = new(
                    ["Gauntlets", "War Gauntlets", "Heavy Gauntlets"],
                    4,
                    5,
                    15
                ),
            },
        };

    private readonly ModifierTemplate[] _modifiers =
    [
        new(
            1,
            ModifierKey.MaxHp,
            100,
            level => new AttributeModifier
            {
                Attribute = AttributeName.MaximumHp,
                AmountType = AmountType.Flat,
                Amount = Roll(level, 5, 100),
            }
        ),
        new(
            1,
            ModifierKey.MaxAp,
            80,
            level => new AttributeModifier
            {
                Attribute = AttributeName.MaximumAp,
                AmountType = AmountType.Flat,
                Amount = Roll(level, 3, 60),
            }
        ),
        new(
            1,
            ModifierKey.Defense,
            100,
            level => new AttributeModifier
            {
                Attribute = AttributeName.Defense,
                AmountType = AmountType.Flat,
                Amount = Roll(level, 2, 40),
            }
        ),
        new(
            1,
            ModifierKey.FireResistance,
            80,
            level => new AttributeModifier
            {
                Attribute = AttributeName.FireResistance,
                AmountType = AmountType.Percent,
                Amount = Roll(level, 5, 40),
            }
        ),
        new(
            1,
            ModifierKey.IceResistance,
            80,
            level => new AttributeModifier
            {
                Attribute = AttributeName.IceResistance,
                AmountType = AmountType.Percent,
                Amount = Roll(level, 5, 40),
            }
        ),
        new(
            1,
            ModifierKey.LightningResistance,
            80,
            level => new AttributeModifier
            {
                Attribute = AttributeName.LightningResistance,
                AmountType = AmountType.Percent,
                Amount = Roll(level, 5, 40),
            }
        ),
        new(
            5,
            ModifierKey.PoisonResistance,
            60,
            level => new AttributeModifier
            {
                Attribute = AttributeName.PoisonResistance,
                AmountType = AmountType.Percent,
                Amount = Roll(level, 5, 40),
            }
        ),
        new(
            5,
            ModifierKey.FasterHitRecovery,
            50,
            level => new CombatSpeedModifier
            {
                SpeedType = CombatSpeedType.FasterHitRecovery,
                Amount = Roll(level, 5, 30),
            }
        ),
        new(
            5,
            ModifierKey.Strength,
            60,
            level => new AttributeModifier
            {
                Attribute = AttributeName.Strength,
                AmountType = AmountType.Flat,
                Amount = Roll(level, 1, 10),
            }
        ),
        new(
            5,
            ModifierKey.Endurance,
            60,
            level => new AttributeModifier
            {
                Attribute = AttributeName.Endurance,
                AmountType = AmountType.Flat,
                Amount = Roll(level, 1, 10),
            }
        ),
        new(
            15,
            ModifierKey.MagicResistance,
            20,
            level => new AttributeModifier
            {
                Attribute = AttributeName.MagicResistance,
                AmountType = AmountType.Percent,
                Amount = Roll(level, 5, 30),
            }
        ),
        new(
            20,
            ModifierKey.ProcWhenStruck,
            5,
            level => new ProcModifier
            {
                AbilityName = AbilityCatalog.Abilities.RandomAttackAbility(),
                Chance = Roll(level, 5, 15) / 100f,
                Trigger = ProcTrigger.WhenStruck,
            }
        ),
    ];

    public Armor GenerateArmor(ArmorType type, ArmorClass armorClass, int level, Guid worldId)
    {
        var fallback = new ArmorClassData([type.ToString()], 5, 2, 20);
        var data = Types.GetValueOrDefault(type)?.GetValueOrDefault(armorClass) ?? fallback;
        var baseName = data.BaseNames[Random.Shared.Next(data.BaseNames.Length)];
        var eligible = _modifiers.Where(t => t.MinItemLevel <= level).ToList();
        var chosen = PickModifierTemplates(eligible, ModifierCount(level), level);
        var modifiers = chosen.Select(t => t.Build(level)).ToList();
        var durabilityMax = 60 + level * 6;

        return new Armor
        {
            WorldId = worldId,
            Level = level,
            Rarity = ItemRarity.Normal,
            Type = type,
            ArmorClass = armorClass,
            Name = BuildName(baseName, chosen),
            Description = "",
            Weight = data.Weight,
            GoldValue = level * 10 + modifiers.Count * 50 + Random.Shared.Next(level * 5 + 1),
            Modifiers = modifiers,
            Defense = Roll(level, data.DefenseLow, data.DefenseHigh),
            DurabilityMax = durabilityMax,
            DurabilityCurrent = durabilityMax,
        };
    }

    public Shield GenerateShield(int level, Guid worldId)
    {
        var baseName = ShieldBaseNames[Random.Shared.Next(ShieldBaseNames.Length)];
        var eligible = _modifiers.Where(t => t.MinItemLevel <= level).ToList();
        var chosen = PickModifierTemplates(eligible, ModifierCount(level), level);
        var modifiers = chosen.Select(t => t.Build(level)).ToList();
        modifiers.AddRange(GenerateShieldResistanceModifiers(level));
        var durabilityMax = 60 + level * 6;

        return new Shield
        {
            WorldId = worldId,
            Level = level,
            Rarity = ItemRarity.Normal,
            Name = BuildName(baseName, chosen),
            Description = "",
            Weight = 8,
            GoldValue = level * 10 + modifiers.Count * 50 + Random.Shared.Next(level * 5 + 1),
            Modifiers = modifiers,
            Defense = Roll(level, 3, 25),
            BlockChance = Roll(level, 10, 50) / 100f,
            DurabilityMax = durabilityMax,
            DurabilityCurrent = durabilityMax,
        };
    }

    private static IReadOnlyCollection<AttributeModifier> GenerateShieldResistanceModifiers(
        int level
    ) =>
        [
            MakeShieldResistanceModifier(AttributeName.MagicResistance, level),
            MakeShieldResistanceModifier(AttributeName.FireResistance, level),
            MakeShieldResistanceModifier(AttributeName.IceResistance, level),
            MakeShieldResistanceModifier(AttributeName.LightningResistance, level),
            MakeShieldResistanceModifier(AttributeName.PoisonResistance, level),
        ];

    private static AttributeModifier MakeShieldResistanceModifier(
        AttributeName attribute,
        int level
    ) =>
        new()
        {
            Attribute = attribute,
            AmountType = AmountType.Flat,
            Amount = Roll(level, 1, 4) / 100f,
        };

    private record ArmorClassData(string[] BaseNames, int Weight, int DefenseLow, int DefenseHigh);
}
