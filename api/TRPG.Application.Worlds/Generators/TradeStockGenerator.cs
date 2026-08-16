using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Generators;

public class TradeStockGenerator(ItemGenerator itemGenerator)
{
    private const int StartingGold = 500;

    public IReadOnlyCollection<Item> Generate(
        IReadOnlyCollection<Prop> props,
        IReadOnlyCollection<Room> rooms,
        IReadOnlyCollection<Building> buildings,
        Guid worldId
    )
    {
        var buildingTypesByLocation = rooms
            .Join(
                buildings,
                room => room.BuildingId,
                building => building.Id,
                (room, building) => new { room.LocationId, building.BuildingType }
            )
            .ToDictionary(x => x.LocationId, x => x.BuildingType);

        return props
            .OfType<Workstation>()
            .Where(workstation =>
                workstation.WorkstationType == WorkstationType.Trade
                && buildingTypesByLocation.ContainsKey(workstation.LocationId)
            )
            .SelectMany(workstation =>
                AssignToWorkstation(
                    CreateStock(buildingTypesByLocation[workstation.LocationId], worldId),
                    workstation.Id
                )
            )
            .ToArray();
    }

    private IReadOnlyCollection<Item> CreateStock(BuildingType buildingType, Guid worldId) =>
        [CreateGold(worldId), .. CreateSellableStock(buildingType, worldId)];

    private IReadOnlyCollection<Item> CreateSellableStock(
        BuildingType buildingType,
        Guid worldId
    ) =>
        buildingType switch
        {
            BuildingType.Blacksmith => CreateBlacksmithStock(worldId),
            BuildingType.GeneralGoods => CreateGeneralGoodsStock(worldId),
            BuildingType.Apothecary => CreateApothecaryStock(worldId),
            BuildingType.ArcaneShop => CreateArcaneShopStock(worldId),
            BuildingType.Tailor => CreateTailorStock(worldId),
            BuildingType.Carpenter => CreateCarpenterStock(worldId),
            BuildingType.Jeweler => CreateJewelerStock(worldId),
            BuildingType.GuildHall or BuildingType.Barracks or BuildingType.Castle =>
                CreateMilitaryStock(worldId),
            _ => [],
        };

    private IReadOnlyCollection<Item> CreateBlacksmithStock(Guid worldId) =>
        [
            CreateWeapon(WeaponType.Sword, worldId),
            CreateWeapon(WeaponType.Axe, worldId),
            CreateWeapon(WeaponType.Mace, worldId),
            CreateArmor(ArmorType.Helm, ArmorClass.Mail, worldId),
            CreateArmor(ArmorType.Chest, ArmorClass.Mail, worldId),
            CreateShield(worldId),
            CreateAmmo(AmmoType.Arrow, 20, worldId),
            CreateAmmo(AmmoType.Bolt, 20, worldId),
        ];

    private IReadOnlyCollection<Item> CreateGeneralGoodsStock(Guid worldId) =>
        [
            CreateWeapon(WeaponType.Dagger, worldId),
            CreateWeapon(WeaponType.Bow, worldId),
            CreateArmor(ArmorType.Chest, ArmorClass.Leather, worldId),
            CreateArmor(ArmorType.Boots, ArmorClass.Leather, worldId),
            CreatePotion(ResourceType.Hp, 5, worldId),
            CreatePotion(ResourceType.Ap, 5, worldId),
            CreatePotion(ResourceType.Mp, 5, worldId),
            CreateAmmo(AmmoType.Arrow, 20, worldId),
            CreateAmmo(AmmoType.Bolt, 20, worldId),
        ];

    private IReadOnlyCollection<Item> CreateApothecaryStock(Guid worldId) =>
        [
            CreatePotion(ResourceType.Hp, 10, worldId),
            CreatePotion(ResourceType.Ap, 10, worldId),
            CreatePotion(ResourceType.Mp, 10, worldId),
        ];

    private IReadOnlyCollection<Item> CreateArcaneShopStock(Guid worldId) =>
        [
            CreateWeapon(WeaponType.Staff, worldId),
            CreateWeapon(WeaponType.Wand, worldId),
            CreateAccessory(AccessoryType.Necklace, worldId),
            CreateAccessory(AccessoryType.Ring, worldId),
            CreatePotion(ResourceType.Mp, 10, worldId),
        ];

    private IReadOnlyCollection<Item> CreateTailorStock(Guid worldId) =>
        [
            CreateArmor(ArmorType.Chest, ArmorClass.Cloth, worldId),
            CreateArmor(ArmorType.Boots, ArmorClass.Cloth, worldId),
            CreateArmor(ArmorType.Gloves, ArmorClass.Cloth, worldId),
            CreateArmor(ArmorType.Chest, ArmorClass.Leather, worldId),
            CreateArmor(ArmorType.Boots, ArmorClass.Leather, worldId),
            CreateArmor(ArmorType.Gloves, ArmorClass.Leather, worldId),
        ];

    private IReadOnlyCollection<Item> CreateCarpenterStock(Guid worldId) =>
        [
            CreateWeapon(WeaponType.Bow, worldId),
            CreateWeapon(WeaponType.Crossbow, worldId),
            CreateShield(worldId),
            CreateAmmo(AmmoType.Arrow, 20, worldId),
            CreateAmmo(AmmoType.Bolt, 20, worldId),
        ];

    private IReadOnlyCollection<Item> CreateJewelerStock(Guid worldId) =>
        [
            CreateAccessory(AccessoryType.Necklace, worldId),
            CreateAccessory(AccessoryType.Ring, worldId),
            CreateAccessory(AccessoryType.Ring, worldId),
            CreateAccessory(AccessoryType.Belt, worldId),
        ];

    private IReadOnlyCollection<Item> CreateMilitaryStock(Guid worldId) =>
        [
            CreateWeapon(WeaponType.Sword, worldId),
            CreateArmor(ArmorType.Chest, ArmorClass.Leather, worldId),
            CreateShield(worldId),
            CreateAmmo(AmmoType.Arrow, 20, worldId),
            CreateAmmo(AmmoType.Bolt, 20, worldId),
        ];

    private static Gold CreateGold(Guid worldId) =>
        new()
        {
            WorldId = worldId,
            Name = "Gold",
            Quantity = StartingGold,
        };

    private Weapon CreateWeapon(WeaponType type, Guid worldId) =>
        SetQuantity(itemGenerator.GenerateWeapon(type, level: 1, worldId), 1);

    private Armor CreateArmor(ArmorType type, ArmorClass armorClass, Guid worldId) =>
        SetQuantity(itemGenerator.GenerateArmor(type, armorClass, level: 1, worldId), 1);

    private Shield CreateShield(Guid worldId) =>
        SetQuantity(itemGenerator.GenerateShield(level: 1, worldId), 1);

    private Accessory CreateAccessory(AccessoryType type, Guid worldId) =>
        SetQuantity(itemGenerator.GenerateAccessory(type, level: 1, worldId), 1);

    private Consumable CreatePotion(ResourceType resource, int quantity, Guid worldId) =>
        SetQuantity(itemGenerator.GenerateConsumable(resource, level: 1, worldId), quantity);

    private Ammunition CreateAmmo(AmmoType type, int quantity, Guid worldId) =>
        SetQuantity(itemGenerator.GenerateAmmo(type, worldId), quantity);

    private static T SetQuantity<T>(T item, int quantity)
        where T : Item
    {
        item.Quantity = quantity;
        return item;
    }

    private static IReadOnlyCollection<Item> AssignToWorkstation(
        IReadOnlyCollection<Item> items,
        Guid workstationId
    )
    {
        foreach (var item in items)
        {
            item.Ownership.OwnerId = workstationId;
            item.Ownership.OwnerType = OwnerType.Workstation;
        }

        return items;
    }
}
