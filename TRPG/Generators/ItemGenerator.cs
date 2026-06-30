using TRPG.Models;

namespace TRPG.Generators;

internal class ItemGenerator(
    WeaponGenerator weaponGenerator,
    ArmorGenerator armorGenerator,
    AccessoryGenerator accessoryGenerator,
    ConsumableGenerator consumableGenerator,
    AmmoGenerator ammoGenerator
) {
    public WeaponItem GenerateWeapon(WeaponType type, int level, Guid worldId) {
        return weaponGenerator.Generate(type, level, worldId);
    }

    public ArmorItem GenerateArmor(ArmorType type, int level, Guid worldId) {
        return armorGenerator.GenerateArmor(type, level, worldId);
    }

    public ShieldItem GenerateShield(int level, Guid worldId) {
        return armorGenerator.GenerateShield(level, worldId);
    }

    public AccessoryItem GenerateAccessory(AccessoryType type, int level, Guid worldId) {
        return accessoryGenerator.Generate(type, level, worldId);
    }

    public ConsumableItem GenerateConsumable(int level, Guid worldId) {
        return consumableGenerator.Generate(level, worldId);
    }

    public AmmunitionItem GenerateAmmo(AmmoType type, Guid worldId) {
        return ammoGenerator.Generate(type, worldId);
    }
}