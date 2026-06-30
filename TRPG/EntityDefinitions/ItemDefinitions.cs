using TRPG.Models;

namespace TRPG.EntityDefinitions;

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

internal class ItemDefinitions(
    ModifierTemplate[] weaponModifiers,
    ModifierTemplate[] armorModifiers,
    ModifierTemplate[] shieldModifiers,
    ModifierTemplate[] accessoryModifiers) {
    private static readonly string[] WeaponPrefixes =
        ["Jagged", "Fine", "Cruel", "Deadly", "Sharp", "Ancient", "Rusted", "Gleaming", "Cursed", "Blessed"];

    private static readonly string[] ArmorPrefixes =
        ["Sturdy", "Battered", "Reinforced", "Hardened", "Ancient", "Fine", "Heavy", "Light", "Enchanted", "Rusted"];

    private static readonly string[] AccessoryPrefixes =
        ["Carved", "Etched", "Worn", "Polished", "Ancient", "Ornate", "Simple", "Enchanted", "Crude", "Fine"];

    private static readonly Dictionary<WeaponType, WeaponData> Weapons = new() {
        [WeaponType.Dagger] = new(
            BaseNames: ["Dagger", "Dirk", "Stiletto", "Kris", "Knife"],
            Weight: 2, MinimumDamageLow: 1, MinimumDamageHigh: 5, MaximumDamageLow: 5, MaximumDamageHigh: 20,
            Range: 1, AttackSpeed: 10),
        [WeaponType.Sword] = new(
            BaseNames: ["Longsword", "Broadsword", "Falchion", "Saber", "Rapier", "Claymore", "Scimitar", "Shortsword"],
            Weight: 8, MinimumDamageLow: 3, MinimumDamageHigh: 8, MaximumDamageLow: 10, MaximumDamageHigh: 35,
            Range: 1, AttackSpeed: 7),
        [WeaponType.Axe] = new(
            BaseNames: ["Hand Axe", "Battle Axe", "War Axe", "Hatchet", "Broad Axe"],
            Weight: 10, MinimumDamageLow: 4, MinimumDamageHigh: 10, MaximumDamageLow: 12, MaximumDamageHigh: 40,
            Range: 1, AttackSpeed: 6),
        [WeaponType.Mace] = new(
            BaseNames: ["Mace", "Morning Star", "Flail", "Club", "War Club"],
            Weight: 9, MinimumDamageLow: 3, MinimumDamageHigh: 8, MaximumDamageLow: 10, MaximumDamageHigh: 35,
            Range: 1, AttackSpeed: 6),
        [WeaponType.Hammer] = new(
            BaseNames: ["Warhammer", "Maul", "Great Hammer", "Sledgehammer"],
            Weight: 12, MinimumDamageLow: 5, MinimumDamageHigh: 12, MaximumDamageLow: 15, MaximumDamageHigh: 45,
            Range: 1, AttackSpeed: 5),
        [WeaponType.Staff] = new(
            BaseNames: ["Oak Staff", "Gnarled Staff", "Battle Staff", "War Staff", "Crystal Staff", "Runed Staff"],
            Weight: 6, MinimumDamageLow: 2, MinimumDamageHigh: 6, MaximumDamageLow: 8, MaximumDamageHigh: 25,
            Range: 2, AttackSpeed: 7),
        [WeaponType.Wand] = new(
            BaseNames: ["Wand", "Rod", "Scepter", "Bone Wand", "Grim Wand"],
            Weight: 2, MinimumDamageLow: 1, MinimumDamageHigh: 4, MaximumDamageLow: 4, MaximumDamageHigh: 15,
            Range: 4, AttackSpeed: 9),
        [WeaponType.Bow] = new(
            BaseNames: ["Short Bow", "Long Bow", "Composite Bow", "Hunting Bow", "War Bow", "Recurve Bow"],
            Weight: 5, MinimumDamageLow: 2, MinimumDamageHigh: 6, MaximumDamageLow: 8, MaximumDamageHigh: 30,
            Range: 15, AttackSpeed: 6),
        [WeaponType.Crossbow] = new(
            BaseNames: ["Light Crossbow", "Heavy Crossbow", "Repeating Crossbow"],
            Weight: 7, MinimumDamageLow: 4, MinimumDamageHigh: 10, MaximumDamageLow: 12, MaximumDamageHigh: 40,
            Range: 18, AttackSpeed: 4),
        [WeaponType.Javelin] = new(
            BaseNames: ["Javelin", "Pilum", "War Dart", "Throwing Spear"],
            Weight: 4, MinimumDamageLow: 3, MinimumDamageHigh: 8, MaximumDamageLow: 10, MaximumDamageHigh: 35,
            Range: 6, AttackSpeed: 7)
    };

    private static readonly Dictionary<ArmorType, ArmorData> Armors = new() {
        [ArmorType.Helm] = new(
            BaseNames: ["Cap", "Helm", "Great Helm", "Crown", "Skull Cap", "War Helm", "Visor"],
            Weight: 6, DefenseLow: 2, DefenseHigh: 15),
        [ArmorType.Chest] = new(
            BaseNames: ["Leather Armor", "Ring Mail", "Chain Mail", "Scale Mail", "Plate Mail", "Full Plate", "Brigandine"],
            Weight: 15, DefenseLow: 5, DefenseHigh: 40),
        [ArmorType.Boots] = new(
            BaseNames: ["Boots", "Greaves", "Sabatons", "Shoes", "War Boots", "Light Boots"],
            Weight: 4, DefenseLow: 1, DefenseHigh: 10),
        [ArmorType.Gloves] = new(
            BaseNames: ["Gloves", "Gauntlets", "Bracers", "Vambraces", "Light Gloves"],
            Weight: 2, DefenseLow: 1, DefenseHigh: 10)
    };

    private static readonly string[] ShieldBaseNames =
        ["Buckler", "Small Shield", "Large Shield", "Tower Shield", "Kite Shield", "Round Shield"];

    private static readonly Dictionary<AccessoryType, AccessoryData> Accessories = new() {
        [AccessoryType.Necklace] = new(BaseNames: ["Amulet", "Pendant", "Medallion", "Talisman", "Charm"], Weight: 1),
        [AccessoryType.Ring] = new(BaseNames: ["Ring", "Band", "Signet", "Seal", "Loop"], Weight: 0),
        [AccessoryType.Belt] = new(BaseNames: ["Belt", "Sash", "Girdle", "War Belt", "Heavy Belt"], Weight: 3)
    };

    private static readonly string[] ConsumableBaseNames =
        ["Health Potion", "Mana Potion", "Antidote", "Elixir", "Tonic"];

    private static readonly Dictionary<AmmoType, string[]> AmmoBaseNames = new() {
        [AmmoType.Arrow] = ["Arrows", "Bodkin Arrows", "Broadhead Arrows", "Fire Arrows"],
        [AmmoType.Bolt] = ["Bolts", "Broadhead Bolts", "Steel Bolts"]
    };

    public static ItemDefinitions Create(AbilityDefinitions abilityDefinitions) {
        string RandomAttackAbility() {
            var attacks = abilityDefinitions.Abilities.OfType<AttackAbility>().ToList();
            return attacks[Random.Shared.Next(attacks.Count)].Name;
        }
        return new ItemDefinitions(
            CreateWeaponModifierPool(RandomAttackAbility),
            CreateArmorModifierPool(RandomAttackAbility),
            CreateArmorModifierPool(RandomAttackAbility),
            CreateAccessoryModifierPool()
        );
    }

    private static ModifierTemplate[] CreateWeaponModifierPool(Func<string> randomAttackAbility) => [
        new ModifierTemplate(1, ModifierKey.Strength, 100,
            level => new AttributeModifier
                { Attribute = AttributeName.Strength, Type = AmountType.Flat, Amount = Roll(level, 1, 15) }),
        new ModifierTemplate(1, ModifierKey.Dexterity, 100,
            level => new AttributeModifier
                { Attribute = AttributeName.Dexterity, Type = AmountType.Flat, Amount = Roll(level, 1, 15) }),
        new ModifierTemplate(5, ModifierKey.IncreasedAttackSpeed, 50,
            level => new CombatSpeedModifier
                { SpeedType = CombatSpeedType.IncreasedAttackSpeed, Amount = Roll(level, 5, 30) }),
        new ModifierTemplate(5, ModifierKey.FireDamage, 80,
            level => new ElementalDamageModifier {
                DamageType = DamageType.Fire, MinDamage = Roll(level, 1, 20), MaxDamage = Roll(level, 5, 60)
            }),
        new ModifierTemplate(5, ModifierKey.IceDamage, 80,
            level => new ElementalDamageModifier {
                DamageType = DamageType.Ice, MinDamage = Roll(level, 1, 20), MaxDamage = Roll(level, 5, 60)
            }),
        new ModifierTemplate(5, ModifierKey.LightningDamage, 80,
            level => new ElementalDamageModifier {
                DamageType = DamageType.Lightning, MinDamage = Roll(level, 1, 20), MaxDamage = Roll(level, 5, 60)
            }),
        new ModifierTemplate(10, ModifierKey.PoisonDamage, 60,
            level => new ElementalDamageModifier {
                DamageType = DamageType.Poison, MinDamage = Roll(level, 1, 15), MaxDamage = Roll(level, 3, 40)
            }),
        new ModifierTemplate(10, ModifierKey.LifeLeech, 40,
            level => new LeechModifier { LeechType = LeechType.Life, Percent = Roll(level, 2, 12) }),
        new ModifierTemplate(10, ModifierKey.ManaLeech, 30,
            level => new LeechModifier { LeechType = LeechType.Mana, Percent = Roll(level, 2, 12) }),
        new ModifierTemplate(15, ModifierKey.DeadlyStrike, 20,
            level => new SpecialHitModifier
                { HitType = SpecialHitType.DeadlyStrike, Chance = Roll(level, 5, 35) }),
        new ModifierTemplate(15, ModifierKey.OpenWounds, 20,
            level => new SpecialHitModifier
                { HitType = SpecialHitType.OpenWounds, Chance = Roll(level, 5, 35) }),
        new ModifierTemplate(20, ModifierKey.CrushingBlow, 10,
            level => new SpecialHitModifier
                { HitType = SpecialHitType.CrushingBlow, Chance = Roll(level, 5, 25) }),
        new ModifierTemplate(25, ModifierKey.ProcOnStriking, 5,
            level => new ProcModifier {
                AbilityName = randomAttackAbility(), Chance = Roll(level, 5, 20),
                Trigger = ProcTrigger.OnStriking
            })
    ];

    private static ModifierTemplate[] CreateArmorModifierPool(Func<string> randomAttackAbility) => [
        new ModifierTemplate(1, ModifierKey.MaxHp, 100,
            level => new AttributeModifier
                { Attribute = AttributeName.MaximumHp, Type = AmountType.Flat, Amount = Roll(level, 5, 100) }),
        new ModifierTemplate(1, ModifierKey.MaxAp, 80,
            level => new AttributeModifier
                { Attribute = AttributeName.MaximumAp, Type = AmountType.Flat, Amount = Roll(level, 3, 60) }),
        new ModifierTemplate(1, ModifierKey.Defense, 100,
            level => new AttributeModifier
                { Attribute = AttributeName.Defense, Type = AmountType.Flat, Amount = Roll(level, 2, 40) }),
        new ModifierTemplate(1, ModifierKey.FireResistance, 80,
            level => new AttributeModifier {
                Attribute = AttributeName.FireResistance, Type = AmountType.Percent, Amount = Roll(level, 5, 40)
            }),
        new ModifierTemplate(1, ModifierKey.IceResistance, 80,
            level => new AttributeModifier {
                Attribute = AttributeName.IceResistance, Type = AmountType.Percent, Amount = Roll(level, 5, 40)
            }),
        new ModifierTemplate(1, ModifierKey.LightningResistance, 80,
            level => new AttributeModifier {
                Attribute = AttributeName.LightningResistance, Type = AmountType.Percent,
                Amount = Roll(level, 5, 40)
            }),
        new ModifierTemplate(5, ModifierKey.PoisonResistance, 60,
            level => new AttributeModifier {
                Attribute = AttributeName.PoisonResistance, Type = AmountType.Percent, Amount = Roll(level, 5, 40)
            }),
        new ModifierTemplate(5, ModifierKey.FasterHitRecovery, 50,
            level => new CombatSpeedModifier
                { SpeedType = CombatSpeedType.FasterHitRecovery, Amount = Roll(level, 5, 30) }),
        new ModifierTemplate(5, ModifierKey.Strength, 60,
            level => new AttributeModifier
                { Attribute = AttributeName.Strength, Type = AmountType.Flat, Amount = Roll(level, 1, 10) }),
        new ModifierTemplate(5, ModifierKey.Endurance, 60,
            level => new AttributeModifier
                { Attribute = AttributeName.Endurance, Type = AmountType.Flat, Amount = Roll(level, 1, 10) }),
        new ModifierTemplate(15, ModifierKey.MagicResistance, 20,
            level => new AttributeModifier {
                Attribute = AttributeName.MagicResistance, Type = AmountType.Percent, Amount = Roll(level, 5, 30)
            }),
        new ModifierTemplate(20, ModifierKey.ProcWhenStruck, 5,
            level => new ProcModifier {
                AbilityName = randomAttackAbility(), Chance = Roll(level, 5, 15),
                Trigger = ProcTrigger.WhenStruck
            })
    ];

    private static ModifierTemplate[] CreateAccessoryModifierPool() => [
        new ModifierTemplate(1, ModifierKey.Strength, 80,
            level => new AttributeModifier
                { Attribute = AttributeName.Strength, Type = AmountType.Flat, Amount = Roll(level, 1, 8) }),
        new ModifierTemplate(1, ModifierKey.Dexterity, 80,
            level => new AttributeModifier
                { Attribute = AttributeName.Dexterity, Type = AmountType.Flat, Amount = Roll(level, 1, 8) }),
        new ModifierTemplate(1, ModifierKey.Intelligence, 80,
            level => new AttributeModifier
                { Attribute = AttributeName.Intelligence, Type = AmountType.Flat, Amount = Roll(level, 1, 8) }),
        new ModifierTemplate(1, ModifierKey.MaxHp, 100,
            level => new AttributeModifier
                { Attribute = AttributeName.MaximumHp, Type = AmountType.Flat, Amount = Roll(level, 3, 50) }),
        new ModifierTemplate(1, ModifierKey.MaxAp, 80,
            level => new AttributeModifier
                { Attribute = AttributeName.MaximumAp, Type = AmountType.Flat, Amount = Roll(level, 3, 50) }),
        new ModifierTemplate(1, ModifierKey.FireResistance, 70,
            level => new AttributeModifier {
                Attribute = AttributeName.FireResistance, Type = AmountType.Percent, Amount = Roll(level, 5, 30)
            }),
        new ModifierTemplate(1, ModifierKey.IceResistance, 70,
            level => new AttributeModifier {
                Attribute = AttributeName.IceResistance, Type = AmountType.Percent, Amount = Roll(level, 5, 30)
            }),
        new ModifierTemplate(1, ModifierKey.LightningResistance, 70,
            level => new AttributeModifier {
                Attribute = AttributeName.LightningResistance, Type = AmountType.Percent,
                Amount = Roll(level, 5, 30)
            }),
        new ModifierTemplate(5, ModifierKey.PoisonResistance, 50,
            level => new AttributeModifier {
                Attribute = AttributeName.PoisonResistance, Type = AmountType.Percent, Amount = Roll(level, 5, 30)
            }),
        new ModifierTemplate(5, ModifierKey.MagicResistance, 20,
            level => new AttributeModifier {
                Attribute = AttributeName.MagicResistance, Type = AmountType.Percent, Amount = Roll(level, 5, 25)
            }),
        new ModifierTemplate(10, ModifierKey.SkillBonus, 15,
            _ => new SkillBonusModifier { Skill = null, Amount = 1 }),
        new ModifierTemplate(20, ModifierKey.FasterCastRate, 10,
            level => new CombatSpeedModifier
                { SpeedType = CombatSpeedType.FasterCastRate, Amount = Roll(level, 5, 20) })
    ];

    public WeaponItem GenerateWeapon(WeaponType type, int level, Guid worldId) {
        var weapon = Weapons.GetValueOrDefault(type, new WeaponData([type.ToString()], 6, 2, 6, 8, 25, 1, 7));
        var baseName = weapon.BaseNames[Random.Shared.Next(weapon.BaseNames.Length)];
        var prefix = WeaponPrefixes[Random.Shared.Next(WeaponPrefixes.Length)];
        var eligible = weaponModifiers.Where(t => t.MinItemLevel <= level).ToList();
        var modifiers = PickModifiers(eligible, ModifierCount(level), level);
        var durabilityMax = 50 + level * 5;

        return new WeaponItem {
            WorldId = worldId,
            Level = level,
            Rarity = ItemRarity.Normal,
            Type = type,
            Name = $"{prefix} {baseName}",
            Description = "",
            Weight = weapon.Weight,
            GoldValue = level * 10 + modifiers.Count * 50 + Random.Shared.Next(level * 5 + 1),
            Modifiers = modifiers,
            MinDamage = Roll(level, weapon.MinimumDamageLow, weapon.MinimumDamageHigh),
            MaxDamage = Roll(level, weapon.MaximumDamageLow, weapon.MaximumDamageHigh),
            Range = weapon.Range,
            AttackSpeed = weapon.AttackSpeed,
            DurabilityMax = durabilityMax,
            DurabilityCurrent = durabilityMax
        };
    }

    public ArmorItem GenerateArmor(ArmorType type, int level, Guid worldId) {
        var armor = Armors.GetValueOrDefault(type, new ArmorData([type.ToString()], 5, 2, 20));
        var baseName = armor.BaseNames[Random.Shared.Next(armor.BaseNames.Length)];
        var prefix = ArmorPrefixes[Random.Shared.Next(ArmorPrefixes.Length)];
        var eligible = armorModifiers.Where(t => t.MinItemLevel <= level).ToList();
        var modifiers = PickModifiers(eligible, ModifierCount(level), level);
        var durabilityMax = 60 + level * 6;

        return new ArmorItem {
            WorldId = worldId,
            Level = level,
            Rarity = ItemRarity.Normal,
            Type = type,
            Name = $"{prefix} {baseName}",
            Description = "",
            Weight = armor.Weight,
            GoldValue = level * 10 + modifiers.Count * 50 + Random.Shared.Next(level * 5 + 1),
            Modifiers = modifiers,
            Defense = Roll(level, armor.DefenseLow, armor.DefenseHigh),
            DurabilityMax = durabilityMax,
            DurabilityCurrent = durabilityMax
        };
    }

    private ShieldItem GenerateShield(int level, Guid worldId) {
        var baseName = ShieldBaseNames[Random.Shared.Next(ShieldBaseNames.Length)];
        var prefix = ArmorPrefixes[Random.Shared.Next(ArmorPrefixes.Length)];
        var eligible = shieldModifiers.Where(t => t.MinItemLevel <= level).ToList();
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

    public AccessoryItem GenerateAccessory(AccessoryType type, int level, Guid worldId) {
        var accessory = Accessories.GetValueOrDefault(type, new AccessoryData([type.ToString()], 1));
        var baseName = accessory.BaseNames[Random.Shared.Next(accessory.BaseNames.Length)];
        var prefix = AccessoryPrefixes[Random.Shared.Next(AccessoryPrefixes.Length)];
        var eligible = accessoryModifiers.Where(t => t.MinItemLevel <= level).ToList();
        var modifiers = PickModifiers(eligible, ModifierCount(level), level);

        return new AccessoryItem {
            WorldId = worldId,
            Level = level,
            Rarity = ItemRarity.Normal,
            Type = type,
            Name = $"{prefix} {baseName}",
            Description = "",
            Weight = accessory.Weight,
            GoldValue = level * 8 + modifiers.Count * 50 + Random.Shared.Next(level * 5 + 1),
            Modifiers = modifiers
        };
    }

    public static ConsumableItem GenerateConsumable(int level, Guid worldId) {
        var baseName = ConsumableBaseNames[Random.Shared.Next(ConsumableBaseNames.Length)];

        return new ConsumableItem {
            WorldId = worldId,
            Level = level,
            Rarity = ItemRarity.Normal,
            Name = baseName,
            Description = "",
            Weight = 1,
            GoldValue = level * 5 + Random.Shared.Next(11),
            Attribute = AttributeName.CurrentHp,
            Amount = Roll(level, 20, 100),
            Duration = 0
        };
    }

    public static AmmunitionItem GenerateAmmo(AmmoType type, Guid worldId) {
        var names = AmmoBaseNames.GetValueOrDefault(type, [type.ToString()]);
        var baseName = names[Random.Shared.Next(names.Length)];

        return new AmmunitionItem {
            WorldId = worldId,
            Level = 1,
            Rarity = ItemRarity.Normal,
            Type = type,
            Name = baseName,
            Description = "",
            Weight = 2,
            GoldValue = 5
        };
    }

    public StartingInventoryResult GenerateStartingInventory(Profession profession, Guid personId, Guid worldId) {
        var startingItems = GetStartingItems(profession, 1, worldId);
        var items = new List<Item>();
        var inventoryItems = new List<InventoryItem>();
        var index = 0;

        foreach (var (item, quantity) in startingItems) {
            items.Add(item);
            inventoryItems.Add(new InventoryItem {
                PersonId = personId,
                ItemId = item.Id,
                Quantity = quantity,
                Index = index++,
                EquippedSlot = item.DefaultSlot
            });
        }

        return new StartingInventoryResult(items.AsReadOnly(), inventoryItems.AsReadOnly());
    }

    private StartingItem[] GetStartingItems(Profession profession, int level, Guid worldId) =>
        profession switch {
            Profession.Knight => [
                new(GenerateWeapon(WeaponType.Sword, level, worldId), 1),
                new(GenerateArmor(ArmorType.Chest, level, worldId), 1)
            ],
            Profession.Rogue => [
                new(GenerateWeapon(WeaponType.Dagger, level, worldId), 1),
                new(GenerateArmor(ArmorType.Boots, level, worldId), 1)
            ],
            Profession.Ranger => [
                new(GenerateWeapon(WeaponType.Bow, level, worldId), 1),
                new(GenerateAmmo(AmmoType.Arrow, worldId), 20)
            ],
            Profession.Mage => [
                new(GenerateWeapon(WeaponType.Staff, level, worldId), 1),
                new(GenerateConsumable(level, worldId), 3)
            ],
            Profession.Cleric => [
                new(GenerateWeapon(WeaponType.Mace, level, worldId), 1),
                new(GenerateArmor(ArmorType.Helm, level, worldId), 1)
            ],
            Profession.Mercenary => [
                new(GenerateWeapon(WeaponType.Sword, level, worldId), 1),
                new(GenerateShield(level, worldId), 1)
            ],
            Profession.Alchemist => [new(GenerateConsumable(level, worldId), 5)],
            Profession.Blacksmith => [new(GenerateWeapon(WeaponType.Axe, level, worldId), 1)],
            Profession.Scholar => [new(GenerateWeapon(WeaponType.Staff, level, worldId), 1)],
            Profession.Merchant => [new(GenerateConsumable(level, worldId), 3)],
            _ => [new(GenerateConsumable(level, worldId), 1)]
        };

    private static List<ItemModifier> PickModifiers(List<ModifierTemplate> pool, int count, int itemLevel) {
        var remaining = pool.ToList();
        var result = new List<ItemModifier>();
        for (var i = 0; i < count && remaining.Count > 0; i++) {
            var template = WeightedRandom(remaining);
            result.Add(template.Build(itemLevel));
            remaining.RemoveAll(t => t.UniqueKey == template.UniqueKey);
        }

        return result;
    }

    private static ModifierTemplate WeightedRandom(List<ModifierTemplate> pool) {
        var total = pool.Sum(t => t.Weight);
        var roll = Random.Shared.Next(total);
        var cumulative = 0;
        foreach (var t in pool) {
            cumulative += t.Weight;
            if (roll < cumulative) {
                return t;
            }
        }

        return pool[^1];
    }

    private static int ModifierCount(int itemLevel) {
        return itemLevel switch {
            <= 5 => Random.Shared.Next(0, 2),
            <= 15 => Random.Shared.Next(1, 3),
            <= 30 => Random.Shared.Next(2, 4),
            <= 60 => Random.Shared.Next(2, 5),
            _ => Random.Shared.Next(3, 6)
        };
    }

    private static int Roll(int itemLevel, int min, int max) {
        var range = max - min;
        var progress = Math.Clamp(itemLevel / 100f, 0f, 1f);
        var jitter = Random.Shared.NextSingle() * (range * 0.2f);
        return Math.Max(1, (int) MathF.Round(min + range * progress + jitter));
    }
}

internal record StartingInventoryResult(IReadOnlyList<Item> Items, IReadOnlyList<InventoryItem> InventoryItems);

internal record WeaponData(
    string[] BaseNames,
    int Weight,
    int MinimumDamageLow,
    int MinimumDamageHigh,
    int MaximumDamageLow,
    int MaximumDamageHigh,
    int Range,
    int AttackSpeed
);

internal record ArmorData(string[] BaseNames, int Weight, int DefenseLow, int DefenseHigh);

internal record AccessoryData(string[] BaseNames, int Weight);

internal record StartingItem(Item Item, int Quantity);