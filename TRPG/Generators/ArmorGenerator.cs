using TRPG.Definitions;
using TRPG.Models;
using static TRPG.Generators.ItemModifierHelpers;

namespace TRPG.Generators;

internal class ArmorGenerator(AbilityDefinitions abilityDefinitions) {
    private static readonly string[] Prefixes =
        ["Sturdy", "Battered", "Reinforced", "Hardened", "Ancient", "Fine", "Heavy", "Light", "Enchanted", "Rusted"];

    private static readonly string[] ShieldBaseNames =
        ["Buckler", "Small Shield", "Large Shield", "Tower Shield", "Kite Shield", "Round Shield"];

    private record ArmorTypeData(string[] BaseNames, int Weight, int DefenseLow, int DefenseHigh);

    private static readonly Dictionary<ArmorType, ArmorTypeData> Types = new() {
        [ArmorType.Helm] = new ArmorTypeData(
            ["Cap", "Helm", "Great Helm", "Crown", "Skull Cap", "War Helm", "Visor"],
            6, 2, 15),
        [ArmorType.Chest] = new ArmorTypeData(
            ["Leather Armor", "Ring Mail", "Chain Mail", "Scale Mail", "Plate Mail", "Full Plate", "Brigandine"],
            15, 5, 40),
        [ArmorType.Boots] = new ArmorTypeData(
            ["Boots", "Greaves", "Sabatons", "Shoes", "War Boots", "Light Boots"],
            4, 1, 10),
        [ArmorType.Gloves] = new ArmorTypeData(
            ["Gloves", "Gauntlets", "Bracers", "Vambraces", "Light Gloves"],
            2, 1, 10)
    };

    private readonly ModifierTemplate[] _modifiers = CreateModifierPool(abilityDefinitions.RandomAttackAbility);

    private static ModifierTemplate[] CreateModifierPool(Func<string> randomAttackAbility) => [
        new(1, ModifierKey.MaxHp, 100,
            level => new AttributeModifier
                { Attribute = AttributeName.MaximumHp, Type = AmountType.Flat, Amount = Roll(level, 5, 100) }),
        new(1, ModifierKey.MaxAp, 80,
            level => new AttributeModifier
                { Attribute = AttributeName.MaximumAp, Type = AmountType.Flat, Amount = Roll(level, 3, 60) }),
        new(1, ModifierKey.Defense, 100,
            level => new AttributeModifier
                { Attribute = AttributeName.Defense, Type = AmountType.Flat, Amount = Roll(level, 2, 40) }),
        new(1, ModifierKey.FireResistance, 80,
            level => new AttributeModifier
                { Attribute = AttributeName.FireResistance, Type = AmountType.Percent, Amount = Roll(level, 5, 40) }),
        new(1, ModifierKey.IceResistance, 80,
            level => new AttributeModifier
                { Attribute = AttributeName.IceResistance, Type = AmountType.Percent, Amount = Roll(level, 5, 40) }),
        new(1, ModifierKey.LightningResistance, 80,
            level => new AttributeModifier {
                Attribute = AttributeName.LightningResistance, Type = AmountType.Percent, Amount = Roll(level, 5, 40)
            }),
        new(5, ModifierKey.PoisonResistance, 60,
            level => new AttributeModifier
                { Attribute = AttributeName.PoisonResistance, Type = AmountType.Percent, Amount = Roll(level, 5, 40) }),
        new(5, ModifierKey.FasterHitRecovery, 50,
            level => new CombatSpeedModifier
                { SpeedType = CombatSpeedType.FasterHitRecovery, Amount = Roll(level, 5, 30) }),
        new(5, ModifierKey.Strength, 60,
            level => new AttributeModifier
                { Attribute = AttributeName.Strength, Type = AmountType.Flat, Amount = Roll(level, 1, 10) }),
        new(5, ModifierKey.Endurance, 60,
            level => new AttributeModifier
                { Attribute = AttributeName.Endurance, Type = AmountType.Flat, Amount = Roll(level, 1, 10) }),
        new(15, ModifierKey.MagicResistance, 20,
            level => new AttributeModifier
                { Attribute = AttributeName.MagicResistance, Type = AmountType.Percent, Amount = Roll(level, 5, 30) }),
        new(20, ModifierKey.ProcWhenStruck, 5,
            level => new ProcModifier
                { AbilityName = randomAttackAbility(), Chance = Roll(level, 5, 15), Trigger = ProcTrigger.WhenStruck })
    ];

    public ArmorItem GenerateArmor(ArmorType type, int level, Guid worldId) {
        var data = Types.GetValueOrDefault(type, new ArmorTypeData([type.ToString()], 5, 2, 20));
        var baseName = data.BaseNames[Random.Shared.Next(data.BaseNames.Length)];
        var prefix = Prefixes[Random.Shared.Next(Prefixes.Length)];
        var eligible = _modifiers.Where(t => t.MinItemLevel <= level).ToList();
        var modifiers = PickModifiers(eligible, ModifierCount(level), level);
        var durabilityMax = 60 + level * 6;

        return new ArmorItem {
            WorldId = worldId,
            Level = level,
            Rarity = ItemRarity.Normal,
            Type = type,
            Name = $"{prefix} {baseName}",
            Description = "",
            Weight = data.Weight,
            GoldValue = level * 10 + modifiers.Count * 50 + Random.Shared.Next(level * 5 + 1),
            Modifiers = modifiers,
            Defense = Roll(level, data.DefenseLow, data.DefenseHigh),
            DurabilityMax = durabilityMax,
            DurabilityCurrent = durabilityMax
        };
    }

    public ShieldItem GenerateShield(int level, Guid worldId) {
        var baseName = ShieldBaseNames[Random.Shared.Next(ShieldBaseNames.Length)];
        var prefix = Prefixes[Random.Shared.Next(Prefixes.Length)];
        var eligible = _modifiers.Where(t => t.MinItemLevel <= level).ToList();
        var modifiers = PickModifiers(eligible, ModifierCount(level), level);
        var durabilityMax = 60 + level * 6;

        return new ShieldItem {
            WorldId = worldId,
            Level = level,
            Rarity = ItemRarity.Normal,
            Name = $"{prefix} {baseName}",
            Description = "",
            Weight = 8,
            GoldValue = level * 10 + modifiers.Count * 50 + Random.Shared.Next(level * 5 + 1),
            Modifiers = modifiers,
            Defense = Roll(level, 3, 25),
            BlockChance = Roll(level, 10, 50),
            DurabilityMax = durabilityMax,
            DurabilityCurrent = durabilityMax
        };
    }
}
