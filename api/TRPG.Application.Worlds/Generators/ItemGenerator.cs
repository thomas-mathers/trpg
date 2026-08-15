using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public class ItemGenerator(
    WeaponGenerator weaponGenerator,
    ArmorGenerator armorGenerator,
    AccessoryGenerator accessoryGenerator,
    ConsumableGenerator consumableGenerator,
    AmmoGenerator ammoGenerator
)
{
    public Weapon GenerateWeapon(WeaponType type, int level, Guid worldId)
    {
        return weaponGenerator.Generate(type, level, worldId);
    }

    public Armor GenerateArmor(ArmorType type, ArmorClass armorClass, int level, Guid worldId)
    {
        return armorGenerator.GenerateArmor(type, armorClass, level, worldId);
    }

    public Shield GenerateShield(int level, Guid worldId)
    {
        return armorGenerator.GenerateShield(level, worldId);
    }

    public Accessory GenerateAccessory(AccessoryType type, int level, Guid worldId)
    {
        return accessoryGenerator.Generate(type, level, worldId);
    }

    public Consumable GenerateConsumable(int level, Guid worldId)
    {
        return consumableGenerator.Generate(level, worldId);
    }

    public Consumable GenerateConsumable(ResourceType resource, int level, Guid worldId)
    {
        return consumableGenerator.Generate(resource, level, worldId);
    }

    public Ammunition GenerateAmmo(AmmoType type, Guid worldId)
    {
        return ammoGenerator.Generate(type, worldId);
    }
}
