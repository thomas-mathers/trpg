using TRPG.Data.Models;
using static TRPG.Application.Worlds.Generators.ItemModifierHelpers;

namespace TRPG.Application.Worlds.Generators;

public class AccessoryGenerator
{
    private static readonly Dictionary<AccessoryType, AccessoryTypeData> Types = new()
    {
        [AccessoryType.Necklace] = new AccessoryTypeData(
            ["Amulet", "Pendant", "Medallion", "Talisman", "Charm"],
            1
        ),
        [AccessoryType.Ring] = new AccessoryTypeData(["Ring", "Band", "Signet", "Seal", "Loop"], 0),
        [AccessoryType.Belt] = new AccessoryTypeData(
            ["Belt", "Sash", "Girdle", "War Belt", "Heavy Belt"],
            3
        ),
    };

    private static readonly ModifierTemplate[] Modifiers =
    [
        new(
            1,
            ModifierKey.Strength,
            80,
            level => new AttributeModifier
            {
                Attribute = AttributeName.Strength,
                AmountType = AmountType.Flat,
                Amount = Roll(level, 1, 8),
            }
        ),
        new(
            1,
            ModifierKey.Dexterity,
            80,
            level => new AttributeModifier
            {
                Attribute = AttributeName.Dexterity,
                AmountType = AmountType.Flat,
                Amount = Roll(level, 1, 8),
            }
        ),
        new(
            1,
            ModifierKey.Intelligence,
            80,
            level => new AttributeModifier
            {
                Attribute = AttributeName.Intelligence,
                AmountType = AmountType.Flat,
                Amount = Roll(level, 1, 8),
            }
        ),
        new(
            1,
            ModifierKey.MaxHp,
            100,
            level => new AttributeModifier
            {
                Attribute = AttributeName.MaximumHp,
                AmountType = AmountType.Flat,
                Amount = Roll(level, 3, 50),
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
                Amount = Roll(level, 3, 50),
            }
        ),
        new(
            1,
            ModifierKey.FireResistance,
            70,
            level => new AttributeModifier
            {
                Attribute = AttributeName.FireResistance,
                AmountType = AmountType.Percent,
                Amount = Roll(level, 5, 30),
            }
        ),
        new(
            1,
            ModifierKey.IceResistance,
            70,
            level => new AttributeModifier
            {
                Attribute = AttributeName.IceResistance,
                AmountType = AmountType.Percent,
                Amount = Roll(level, 5, 30),
            }
        ),
        new(
            1,
            ModifierKey.LightningResistance,
            70,
            level => new AttributeModifier
            {
                Attribute = AttributeName.LightningResistance,
                AmountType = AmountType.Percent,
                Amount = Roll(level, 5, 30),
            }
        ),
        new(
            5,
            ModifierKey.PoisonResistance,
            50,
            level => new AttributeModifier
            {
                Attribute = AttributeName.PoisonResistance,
                AmountType = AmountType.Percent,
                Amount = Roll(level, 5, 30),
            }
        ),
        new(
            5,
            ModifierKey.MagicResistance,
            20,
            level => new AttributeModifier
            {
                Attribute = AttributeName.MagicResistance,
                AmountType = AmountType.Percent,
                Amount = Roll(level, 5, 25),
            }
        ),
        new(
            10,
            ModifierKey.SkillBonus,
            15,
            _ => new SkillBonusModifier { Skill = null, Amount = 1 }
        ),
        new(
            20,
            ModifierKey.FasterCastRate,
            10,
            level => new CombatSpeedModifier
            {
                SpeedType = CombatSpeedType.FasterCastRate,
                Amount = Roll(level, 5, 20),
            }
        ),
    ];

    public Accessory Generate(AccessoryType type, int level, Guid worldId)
    {
        var data = Types.GetValueOrDefault(type, new AccessoryTypeData([type.ToString()], 1));
        var baseName = data.BaseNames[Random.Shared.Next(data.BaseNames.Length)];
        var eligible = Modifiers.Where(t => t.MinItemLevel <= level).ToList();
        var chosen = PickModifierTemplates(eligible, ModifierCount(level), level);
        var modifiers = chosen.Select(t => t.Build(level)).ToList();

        return new Accessory
        {
            WorldId = worldId,
            Level = level,
            Rarity = ItemRarity.Normal,
            Type = type,
            Name = BuildName(baseName, chosen),
            Description = "",
            Weight = data.Weight,
            GoldValue = level * 8 + modifiers.Count * 50 + Random.Shared.Next(level * 5 + 1),
            Modifiers = modifiers,
        };
    }

    private record AccessoryTypeData(string[] BaseNames, int Weight);
}
