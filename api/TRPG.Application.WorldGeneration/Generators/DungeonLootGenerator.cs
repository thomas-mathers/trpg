using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DungeonLootResult(IReadOnlyList<Prop> Containers, IReadOnlyList<Item> Items);

public class DungeonLootGenerator(ItemGenerator itemGenerator)
{
    private const int MundaneGold = 25;
    private const int ValuableGold = 90;

    private static readonly WeaponType[] Weapons =
    [
        WeaponType.Dagger,
        WeaponType.Sword,
        WeaponType.Axe,
        WeaponType.Mace,
        WeaponType.Bow,
    ];

    private static readonly (ArmorType Type, ArmorClass Class)[] Armors =
    [
        (ArmorType.Helm, ArmorClass.Leather),
        (ArmorType.Chest, ArmorClass.Leather),
        (ArmorType.Boots, ArmorClass.Mail),
        (ArmorType.Chest, ArmorClass.Mail),
    ];

    internal DungeonLootResult Generate(
        IReadOnlyCollection<DungeonRoomPlacement> placements,
        Guid worldId,
        Random random
    )
    {
        var containers = new List<Prop>();
        var items = new List<Item>();

        foreach (var placement in placements)
        {
            var quality = DungeonContentPolicy.Holds(placement.Role, random);
            if (quality == DungeonLootQuality.None)
            {
                continue;
            }

            var container = new Container
            {
                LocationId = placement.Room.LocationId,
                WorldId = worldId,
                Name = ContainerName(quality),
                Description = "Something was left in here, and nobody came back for it.",
            };
            containers.Add(container);
            items.AddRange(
                Fill(container.Id, quality, placement.DepthFromEntrance, worldId, random)
            );
        }

        return new DungeonLootResult(containers, items);
    }

    // Deeper rooms hold better things, which is the only sense of direction a dungeon offers when
    // the player has no map.
    private IEnumerable<Item> Fill(
        Guid containerId,
        DungeonLootQuality quality,
        int depth,
        Guid worldId,
        Random random
    )
    {
        var level = Math.Max(1, depth);
        var items = new List<Item>
        {
            new Gold
            {
                WorldId = worldId,
                Name = "Gold",
                Quantity = Coins(quality, depth, random),
            },
            itemGenerator.GenerateConsumable(level, worldId),
        };

        if (quality == DungeonLootQuality.Valuable)
        {
            items.Add(EquipmentPiece(level, worldId, random));
        }

        foreach (var item in items)
        {
            item.Ownership.OwnerId = containerId;
            item.Ownership.OwnerType = OwnerType.Container;
        }

        return items;
    }

    private Item EquipmentPiece(int level, Guid worldId, Random random)
    {
        if (random.NextDouble() < 0.5)
        {
            return itemGenerator.GenerateWeapon(
                Weapons[random.Next(Weapons.Length)],
                level,
                worldId
            );
        }

        var (type, armorClass) = Armors[random.Next(Armors.Length)];

        return itemGenerator.GenerateArmor(type, armorClass, level, worldId);
    }

    private static int Coins(DungeonLootQuality quality, int depth, Random random)
    {
        var baseline = quality == DungeonLootQuality.Valuable ? ValuableGold : MundaneGold;

        return baseline + depth * 10 + random.Next(baseline);
    }

    private static string ContainerName(DungeonLootQuality quality) =>
        quality == DungeonLootQuality.Valuable ? "Strongbox" : "Crate";
}
