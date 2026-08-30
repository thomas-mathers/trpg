using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Generators;

internal record BuildingStockCatalog(
    IReadOnlyList<WeaponType> WeaponPool,
    int WeaponSlots,
    IReadOnlyList<(ArmorType Type, ArmorClass ArmorClass)> ArmorPool,
    int ArmorSlots,
    IReadOnlyList<AccessoryType> AccessoryPool,
    int AccessorySlots,
    int ShieldSlots,
    IReadOnlyList<(ResourceType Resource, int Quantity)> Potions,
    IReadOnlyList<(AmmoType Type, int Quantity)> Ammo
)
{
    internal static BuildingStockCatalog Empty { get; } = new([], 0, [], 0, [], 0, 0, [], []);
}

public record TradeStockFillResult(
    IReadOnlyCollection<Item> ItemsToAdd,
    IReadOnlyDictionary<Guid, int> QuantityIncreasesByItemId
);

public static class TradeStockFiller
{
    private const int StartingGold = 500;

    private static readonly Dictionary<BuildingType, BuildingStockCatalog> Catalogs = new()
    {
        [BuildingType.Blacksmith] = new BuildingStockCatalog(
            WeaponPool: [WeaponType.Sword, WeaponType.Axe, WeaponType.Mace, WeaponType.Hammer],
            WeaponSlots: 3,
            ArmorPool:
            [
                (ArmorType.Helm, ArmorClass.Mail),
                (ArmorType.Chest, ArmorClass.Mail),
                (ArmorType.Boots, ArmorClass.Mail),
                (ArmorType.Helm, ArmorClass.Plate),
                (ArmorType.Chest, ArmorClass.Plate),
            ],
            ArmorSlots: 2,
            AccessoryPool: [],
            AccessorySlots: 0,
            ShieldSlots: 1,
            Potions: [],
            Ammo: [(AmmoType.Arrow, 20), (AmmoType.Bolt, 20)]
        ),
        [BuildingType.GeneralGoods] = new BuildingStockCatalog(
            WeaponPool: [WeaponType.Dagger, WeaponType.Bow, WeaponType.Sword],
            WeaponSlots: 2,
            ArmorPool:
            [
                (ArmorType.Chest, ArmorClass.Leather),
                (ArmorType.Boots, ArmorClass.Leather),
                (ArmorType.Gloves, ArmorClass.Leather),
                (ArmorType.Helm, ArmorClass.Cloth),
            ],
            ArmorSlots: 2,
            AccessoryPool: [],
            AccessorySlots: 0,
            ShieldSlots: 0,
            Potions: [(ResourceType.Hp, 5), (ResourceType.Ap, 5), (ResourceType.Mp, 5)],
            Ammo: [(AmmoType.Arrow, 20), (AmmoType.Bolt, 20)]
        ),
        [BuildingType.Apothecary] = new BuildingStockCatalog(
            WeaponPool: [],
            WeaponSlots: 0,
            ArmorPool: [],
            ArmorSlots: 0,
            AccessoryPool: [],
            AccessorySlots: 0,
            ShieldSlots: 0,
            Potions: [(ResourceType.Hp, 10), (ResourceType.Ap, 10), (ResourceType.Mp, 10)],
            Ammo: []
        ),
        [BuildingType.ArcaneShop] = new BuildingStockCatalog(
            WeaponPool: [WeaponType.Staff, WeaponType.Wand],
            WeaponSlots: 2,
            ArmorPool: [],
            ArmorSlots: 0,
            AccessoryPool: [AccessoryType.Necklace, AccessoryType.Ring, AccessoryType.Belt],
            AccessorySlots: 2,
            ShieldSlots: 0,
            Potions: [(ResourceType.Mp, 10)],
            Ammo: []
        ),
        [BuildingType.Tailor] = new BuildingStockCatalog(
            WeaponPool: [],
            WeaponSlots: 0,
            ArmorPool:
            [
                (ArmorType.Chest, ArmorClass.Cloth),
                (ArmorType.Boots, ArmorClass.Cloth),
                (ArmorType.Gloves, ArmorClass.Cloth),
                (ArmorType.Helm, ArmorClass.Cloth),
                (ArmorType.Chest, ArmorClass.Leather),
                (ArmorType.Boots, ArmorClass.Leather),
                (ArmorType.Gloves, ArmorClass.Leather),
                (ArmorType.Helm, ArmorClass.Leather),
            ],
            ArmorSlots: 6,
            AccessoryPool: [],
            AccessorySlots: 0,
            ShieldSlots: 0,
            Potions: [],
            Ammo: []
        ),
        [BuildingType.Carpenter] = new BuildingStockCatalog(
            WeaponPool: [WeaponType.Bow, WeaponType.Crossbow, WeaponType.Javelin],
            WeaponSlots: 2,
            ArmorPool: [],
            ArmorSlots: 0,
            AccessoryPool: [],
            AccessorySlots: 0,
            ShieldSlots: 1,
            Potions: [],
            Ammo: [(AmmoType.Arrow, 20), (AmmoType.Bolt, 20)]
        ),
        [BuildingType.Jeweler] = new BuildingStockCatalog(
            WeaponPool: [],
            WeaponSlots: 0,
            ArmorPool: [],
            ArmorSlots: 0,
            AccessoryPool: [AccessoryType.Necklace, AccessoryType.Ring, AccessoryType.Belt],
            AccessorySlots: 4,
            ShieldSlots: 0,
            Potions: [],
            Ammo: []
        ),
        [BuildingType.GuildHall] = new BuildingStockCatalog(
            WeaponPool: [WeaponType.Sword, WeaponType.Axe, WeaponType.Mace],
            WeaponSlots: 1,
            ArmorPool:
            [
                (ArmorType.Chest, ArmorClass.Leather),
                (ArmorType.Helm, ArmorClass.Leather),
            ],
            ArmorSlots: 1,
            AccessoryPool: [],
            AccessorySlots: 0,
            ShieldSlots: 1,
            Potions: [],
            Ammo: [(AmmoType.Arrow, 20), (AmmoType.Bolt, 20)]
        ),
    };

    public static TradeStockFillResult Fill(
        ItemGenerator itemGenerator,
        BuildingType buildingType,
        IReadOnlyCollection<Item> currentItems,
        Guid worldId,
        int playerLevel
    )
    {
        var catalog = Catalogs.GetValueOrDefault(buildingType, BuildingStockCatalog.Empty);
        var itemsToAdd = new List<Item>();
        var quantityIncreases = new Dictionary<Guid, int>();

        FillGold(currentItems, worldId, itemsToAdd, quantityIncreases);
        FillWeapons(itemGenerator, catalog, currentItems, worldId, playerLevel, itemsToAdd);
        FillArmor(itemGenerator, catalog, currentItems, worldId, playerLevel, itemsToAdd);
        FillAccessories(itemGenerator, catalog, currentItems, worldId, playerLevel, itemsToAdd);
        FillShields(itemGenerator, catalog, currentItems, worldId, playerLevel, itemsToAdd);
        FillPotions(itemGenerator, catalog, currentItems, worldId, quantityIncreases, itemsToAdd);
        FillAmmo(itemGenerator, catalog, currentItems, worldId, quantityIncreases, itemsToAdd);

        return new TradeStockFillResult(itemsToAdd, quantityIncreases);
    }

    private static void FillGold(
        IReadOnlyCollection<Item> currentItems,
        Guid worldId,
        List<Item> itemsToAdd,
        Dictionary<Guid, int> quantityIncreases
    )
    {
        var existingGold = currentItems.OfType<Gold>().FirstOrDefault();
        if (existingGold == null)
        {
            itemsToAdd.Add(
                new Gold
                {
                    WorldId = worldId,
                    Name = "Gold",
                    Quantity = StartingGold,
                }
            );
        }
        else if (existingGold.Quantity < StartingGold)
        {
            quantityIncreases[existingGold.Id] = StartingGold;
        }
    }

    private static void FillWeapons(
        ItemGenerator itemGenerator,
        BuildingStockCatalog catalog,
        IReadOnlyCollection<Item> currentItems,
        Guid worldId,
        int playerLevel,
        List<Item> itemsToAdd
    )
    {
        var missing = catalog.WeaponSlots - currentItems.OfType<Weapon>().Count();
        for (var i = 0; i < missing; i++)
        {
            var type = catalog.WeaponPool[Random.Shared.Next(catalog.WeaponPool.Count)];
            itemsToAdd.Add(
                SetQuantity(
                    itemGenerator.GenerateWeapon(type, RollItemLevel(playerLevel), worldId),
                    1
                )
            );
        }
    }

    private static void FillArmor(
        ItemGenerator itemGenerator,
        BuildingStockCatalog catalog,
        IReadOnlyCollection<Item> currentItems,
        Guid worldId,
        int playerLevel,
        List<Item> itemsToAdd
    )
    {
        var missing = catalog.ArmorSlots - currentItems.OfType<Armor>().Count();
        for (var i = 0; i < missing; i++)
        {
            var (type, armorClass) = catalog.ArmorPool[Random.Shared.Next(catalog.ArmorPool.Count)];
            itemsToAdd.Add(
                SetQuantity(
                    itemGenerator.GenerateArmor(
                        type,
                        armorClass,
                        RollItemLevel(playerLevel),
                        worldId
                    ),
                    1
                )
            );
        }
    }

    private static void FillAccessories(
        ItemGenerator itemGenerator,
        BuildingStockCatalog catalog,
        IReadOnlyCollection<Item> currentItems,
        Guid worldId,
        int playerLevel,
        List<Item> itemsToAdd
    )
    {
        var missing = catalog.AccessorySlots - currentItems.OfType<Accessory>().Count();
        for (var i = 0; i < missing; i++)
        {
            var type = catalog.AccessoryPool[Random.Shared.Next(catalog.AccessoryPool.Count)];
            itemsToAdd.Add(
                SetQuantity(
                    itemGenerator.GenerateAccessory(type, RollItemLevel(playerLevel), worldId),
                    1
                )
            );
        }
    }

    private static void FillShields(
        ItemGenerator itemGenerator,
        BuildingStockCatalog catalog,
        IReadOnlyCollection<Item> currentItems,
        Guid worldId,
        int playerLevel,
        List<Item> itemsToAdd
    )
    {
        var missing = catalog.ShieldSlots - currentItems.OfType<Shield>().Count();
        for (var i = 0; i < missing; i++)
        {
            itemsToAdd.Add(
                SetQuantity(itemGenerator.GenerateShield(RollItemLevel(playerLevel), worldId), 1)
            );
        }
    }

    private static void FillPotions(
        ItemGenerator itemGenerator,
        BuildingStockCatalog catalog,
        IReadOnlyCollection<Item> currentItems,
        Guid worldId,
        Dictionary<Guid, int> quantityIncreases,
        List<Item> itemsToAdd
    )
    {
        foreach (var (resource, quantity) in catalog.Potions)
        {
            var existing = currentItems
                .OfType<Consumable>()
                .FirstOrDefault(c => c.Resource == resource);
            if (existing == null)
            {
                itemsToAdd.Add(
                    SetQuantity(
                        itemGenerator.GenerateConsumable(resource, level: 1, worldId),
                        quantity
                    )
                );
            }
            else if (existing.Quantity < quantity)
            {
                quantityIncreases[existing.Id] = quantity;
            }
        }
    }

    private static void FillAmmo(
        ItemGenerator itemGenerator,
        BuildingStockCatalog catalog,
        IReadOnlyCollection<Item> currentItems,
        Guid worldId,
        Dictionary<Guid, int> quantityIncreases,
        List<Item> itemsToAdd
    )
    {
        foreach (var (type, quantity) in catalog.Ammo)
        {
            var existing = currentItems.OfType<Ammunition>().FirstOrDefault(a => a.Type == type);
            if (existing == null)
            {
                itemsToAdd.Add(SetQuantity(itemGenerator.GenerateAmmo(type, worldId), quantity));
            }
            else if (existing.Quantity < quantity)
            {
                quantityIncreases[existing.Id] = quantity;
            }
        }
    }

    private static int RollItemLevel(int playerLevel) =>
        Math.Max(1, playerLevel + Random.Shared.Next(-2, 3));

    private static T SetQuantity<T>(T item, int quantity)
        where T : Item
    {
        item.Quantity = quantity;
        return item;
    }
}
