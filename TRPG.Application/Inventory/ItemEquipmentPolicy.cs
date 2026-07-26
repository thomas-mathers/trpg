using TRPG.Data.Models;

namespace TRPG.Application.Inventory;

internal static class ItemEquipmentPolicy
{
    public static EquipmentSlot? GetDefaultSlot(Item item) =>
        item switch
        {
            Weapon => EquipmentSlot.RightHand,
            Shield => EquipmentSlot.LeftHand,
            Ammunition => EquipmentSlot.LeftHand,
            Armor armor => armor.Type switch
            {
                ArmorType.Helm => EquipmentSlot.Helm,
                ArmorType.Chest => EquipmentSlot.Chest,
                ArmorType.Boots => EquipmentSlot.Boots,
                ArmorType.Gloves => EquipmentSlot.Gloves,
                _ => null,
            },
            Accessory accessory => accessory.Type switch
            {
                AccessoryType.Necklace => EquipmentSlot.Necklace,
                AccessoryType.Belt => EquipmentSlot.Belt,
                AccessoryType.Ring => EquipmentSlot.LeftRing,
                _ => null,
            },
            _ => null,
        };

    public static bool IsStackable(Item item) =>
        item switch
        {
            Consumable or Ammunition or Gold => true,
            Weapon weapon => weapon.Type == WeaponType.Javelin,
            _ => false,
        };

    // Splits a stack in two: used when only part of a stackable item's quantity is being
    // transferred to a new owner. Copies every type-specific field but not Id/Quantity/Ownership,
    // since the caller assigns those to the new instance itself.
    public static Item Clone(Item item) =>
        item switch
        {
            Weapon w => new Weapon
            {
                WorldId = w.WorldId,
                Name = w.Name,
                Description = w.Description,
                Weight = w.Weight,
                Modifiers = [.. w.Modifiers],
                GoldValue = w.GoldValue,
                Level = w.Level,
                Rarity = w.Rarity,
                Type = w.Type,
                MinDamage = w.MinDamage,
                MaxDamage = w.MaxDamage,
                Range = w.Range,
                AttackSpeed = w.AttackSpeed,
                DurabilityCurrent = w.DurabilityCurrent,
                DurabilityMax = w.DurabilityMax,
            },
            Armor a => new Armor
            {
                WorldId = a.WorldId,
                Name = a.Name,
                Description = a.Description,
                Weight = a.Weight,
                Modifiers = [.. a.Modifiers],
                GoldValue = a.GoldValue,
                Level = a.Level,
                Rarity = a.Rarity,
                Type = a.Type,
                ArmorClass = a.ArmorClass,
                Defense = a.Defense,
                DurabilityCurrent = a.DurabilityCurrent,
                DurabilityMax = a.DurabilityMax,
            },
            Shield s => new Shield
            {
                WorldId = s.WorldId,
                Name = s.Name,
                Description = s.Description,
                Weight = s.Weight,
                Modifiers = [.. s.Modifiers],
                GoldValue = s.GoldValue,
                Level = s.Level,
                Rarity = s.Rarity,
                BlockChance = s.BlockChance,
                Defense = s.Defense,
                DurabilityCurrent = s.DurabilityCurrent,
                DurabilityMax = s.DurabilityMax,
            },
            Accessory ac => new Accessory
            {
                WorldId = ac.WorldId,
                Name = ac.Name,
                Description = ac.Description,
                Weight = ac.Weight,
                Modifiers = [.. ac.Modifiers],
                GoldValue = ac.GoldValue,
                Level = ac.Level,
                Rarity = ac.Rarity,
                Type = ac.Type,
            },
            Consumable c => new Consumable
            {
                WorldId = c.WorldId,
                Name = c.Name,
                Description = c.Description,
                Weight = c.Weight,
                GoldValue = c.GoldValue,
                Level = c.Level,
                Rarity = c.Rarity,
                RestoreAmount = c.RestoreAmount,
                Resource = c.Resource,
                Duration = c.Duration,
            },
            Ammunition am => new Ammunition
            {
                WorldId = am.WorldId,
                Name = am.Name,
                Description = am.Description,
                Weight = am.Weight,
                GoldValue = am.GoldValue,
                Level = am.Level,
                Rarity = am.Rarity,
                Type = am.Type,
            },
            Gold g => new Gold
            {
                WorldId = g.WorldId,
                Name = g.Name,
                Description = g.Description,
                Weight = g.Weight,
            },
            _ => new Item
            {
                WorldId = item.WorldId,
                Name = item.Name,
                Description = item.Description,
                Weight = item.Weight,
            },
        };
}
