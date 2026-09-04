using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record RoomSpec(
    string Name,
    string Description,
    int FloorNumber,
    int Capacity,
    PropSpec[] Props
);

internal record PropSpec(string Name, Func<Guid, Guid, Prop> Factory);

internal record IdleDestinationSpec(int Popularity, HourWindow? OpenHours);

internal record BuildingSpec(
    BuildingType Type,
    IReadOnlyList<RoomSpec> Rooms,
    bool IsLockable,
    int LockLevel,
    IdleDestinationSpec? IdleDestination
);

internal static class BuildingSpecCatalog
{
    private static readonly string[] InnGuestRoomDirections = ["North", "South", "East", "West"];

    internal static BuildingSpec GetSpecs(
        BuildingType buildingType,
        Guid? ownerId,
        IReadOnlyList<Guid> memberIds,
        IReadOnlyList<IReadOnlyList<Guid>>? bedroomGroups
    )
    {
        IReadOnlyList<RoomSpec> rooms = buildingType switch
        {
            BuildingType.House => GetHouseSpecs(
                bedroomGroups ?? memberIds.Select(id => (IReadOnlyList<Guid>)[id]).ToArray()
            ),
            BuildingType.Tavern => GetTavernSpecs(ownerId),
            BuildingType.Inn => GetInnSpecs(ownerId),
            BuildingType.Blacksmith => GetBlacksmithSpecs(ownerId),
            BuildingType.Temple => GetTempleSpecs(ownerId),
            BuildingType.Library => GetLibrarySpecs(ownerId),
            BuildingType.GeneralGoods => GetGeneralGoodsSpecs(ownerId),
            BuildingType.Apothecary => GetApothecarySpecs(ownerId),
            BuildingType.Bakery => GetBakerySpecs(ownerId),
            BuildingType.Stable => GetStableSpecs(ownerId),
            BuildingType.Barracks => GetBarracksSpecs(memberIds),
            BuildingType.ArcaneShop => GetArcaneShopSpecs(ownerId),
            BuildingType.GuildHall => GetGuildHallSpecs(ownerId, memberIds),
            BuildingType.Castle => GetCastleSpecs(ownerId),
            BuildingType.Jail => GetJailSpecs(),
            BuildingType.Tailor => GetTailorSpecs(ownerId),
            BuildingType.Carpenter => GetCarpenterSpecs(ownerId),
            BuildingType.Jeweler => GetJewelerSpecs(ownerId),
            _ => throw new InvalidOperationException($"{buildingType} cannot be placed in a city."),
        };

        var isLockable = buildingType is not (BuildingType.Inn or BuildingType.Tavern);

        var lockLevel = buildingType switch
        {
            BuildingType.House
            or BuildingType.GeneralGoods
            or BuildingType.Bakery
            or BuildingType.Tailor
            or BuildingType.Carpenter
            or BuildingType.Stable
            or BuildingType.Inn
            or BuildingType.Tavern => 1,
            BuildingType.Apothecary
            or BuildingType.Blacksmith
            or BuildingType.Jeweler
            or BuildingType.ArcaneShop
            or BuildingType.Library
            or BuildingType.GuildHall
            or BuildingType.Temple => 2,
            BuildingType.Barracks or BuildingType.Castle or BuildingType.Jail => 3,
            _ => 0,
        };

        var idleDestination = buildingType switch
        {
            BuildingType.Jail
            or BuildingType.Castle
            or BuildingType.Barracks
            or BuildingType.GuildHall => null,
            BuildingType.Inn => new IdleDestinationSpec(
                EmploymentAssigner.BuildingPopularity[buildingType],
                null
            ),
            _ => new IdleDestinationSpec(
                EmploymentAssigner.BuildingPopularity[buildingType],
                StaffingPolicy.GetWorkHoursForBuilding(buildingType)
            ),
        };

        return new BuildingSpec(buildingType, rooms, isLockable, lockLevel, idleDestination);
    }

    private static RoomSpec[] GetBlacksmithSpecs(Guid? ownerId)
    {
        return
        [
            new RoomSpec(
                "Workshop",
                "A working forge with tools and equipment for smithing.",
                0,
                6,
                [
                    new PropSpec(
                        "Forge",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Forge",
                                Description = "A roaring forge for shaping metal.",
                                WorkstationType = WorkstationType.Weaponsmithing,
                            }
                    ),
                    new PropSpec(
                        "Anvil",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Anvil",
                                Description = "A heavy anvil for hammering armour.",
                                WorkstationType = WorkstationType.Armorsmithing,
                            }
                    ),
                    new PropSpec(
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A wooden counter for trading.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Living Quarters",
                "Simple living quarters above the shop.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A modest bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A sturdy chest for personal belongings.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetApothecarySpecs(Guid? ownerId)
    {
        return
        [
            new RoomSpec(
                "Shop",
                "A shop filled with the smell of herbs and potions.",
                0,
                6,
                [
                    new PropSpec(
                        "Alchemy Table",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Alchemy Table",
                                Description = "A table covered in alchemical equipment.",
                                WorkstationType = WorkstationType.Alchemy,
                            }
                    ),
                    new PropSpec(
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A counter for selling remedies.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Living Quarters",
                "Modest living quarters above the apothecary.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A modest bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A chest for personal belongings.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetBakerySpecs(Guid? ownerId)
    {
        return
        [
            new RoomSpec(
                "Bakery",
                "A warm room smelling of fresh bread.",
                0,
                6,
                [
                    new PropSpec(
                        "Oven",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Oven",
                                Description = "A large stone oven for baking.",
                                WorkstationType = WorkstationType.Cooking,
                            }
                    ),
                    new PropSpec(
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A counter for selling baked goods.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Living Quarters",
                "Living quarters above the bakery.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A modest bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A chest for personal belongings.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetArcaneShopSpecs(Guid? ownerId)
    {
        return
        [
            new RoomSpec(
                "Shop",
                "A dimly lit shop filled with arcane curiosities.",
                0,
                6,
                [
                    new PropSpec(
                        "Enchanting Table",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Enchanting Table",
                                Description = "A table humming with magical energy.",
                                WorkstationType = WorkstationType.Enchanting,
                            }
                    ),
                    new PropSpec(
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A counter for selling arcane wares.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Living Quarters",
                "Living quarters above the arcane shop.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A modest bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A chest for personal belongings.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetGeneralGoodsSpecs(Guid? ownerId)
    {
        return
        [
            new RoomSpec(
                "Shop",
                "A well-stocked shop with a wide variety of goods.",
                0,
                6,
                [
                    new PropSpec(
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A counter for general trading.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Crate",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Crate",
                                Description = "A large crate of goods.",
                            }
                    ),
                    new PropSpec(
                        "Barrel",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Barrel",
                                Description = "A barrel of supplies.",
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Living Quarters",
                "Living quarters behind the shop.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A modest bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A chest for personal belongings.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetTailorSpecs(Guid? ownerId)
    {
        return
        [
            new RoomSpec(
                "Shop",
                "A shop filled with bolts of cloth and half-finished garments.",
                0,
                6,
                [
                    new PropSpec(
                        "Cutting Table",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Cutting Table",
                                Description = "A table for cutting and measuring fabric.",
                                WorkstationType = WorkstationType.Tailoring,
                            }
                    ),
                    new PropSpec(
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A counter for selling garments.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Living Quarters",
                "Living quarters above the tailor's shop.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A modest bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A chest for personal belongings.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetCarpenterSpecs(Guid? ownerId)
    {
        return
        [
            new RoomSpec(
                "Workshop",
                "A sawdust-covered workshop stacked with timber.",
                0,
                6,
                [
                    new PropSpec(
                        "Workbench",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Workbench",
                                Description = "A sturdy workbench for joining and carving wood.",
                                WorkstationType = WorkstationType.Carpentry,
                            }
                    ),
                    new PropSpec(
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A counter for selling furniture and woodwork.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Living Quarters",
                "Living quarters above the workshop.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A modest bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A chest for personal belongings.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetJewelerSpecs(Guid? ownerId)
    {
        return
        [
            new RoomSpec(
                "Shop",
                "A softly lit shop displaying cut gems and fine settings.",
                0,
                6,
                [
                    new PropSpec(
                        "Jeweler's Bench",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Jeweler's Bench",
                                Description = "A precise bench for cutting and setting gemstones.",
                                WorkstationType = WorkstationType.Jewelcrafting,
                            }
                    ),
                    new PropSpec(
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A counter for selling jewelry.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Living Quarters",
                "Living quarters above the jeweler's shop.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A modest bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A chest for personal belongings.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetLibrarySpecs(Guid? ownerId)
    {
        return
        [
            new RoomSpec(
                "Reading Room",
                "A quiet room lined with shelves of books.",
                0,
                10,
                [
                    new PropSpec(
                        "Bookcase",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bookcase",
                                Description = "A tall bookcase filled with tomes.",
                                WorkstationType = WorkstationType.Reading,
                            }
                    ),
                    new PropSpec(
                        "Bookcase",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bookcase",
                                Description = "A tall bookcase filled with manuscripts.",
                                WorkstationType = WorkstationType.Reading,
                            }
                    ),
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A comfortable reading chair.",
                            }
                    ),
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A comfortable reading chair.",
                            }
                    ),
                    new PropSpec(
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A counter for lending books.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Study",
                "A private study for the librarian.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A modest bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Bookcase",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bookcase",
                                Description = "A personal bookcase.",
                                WorkstationType = WorkstationType.Reading,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A chest for personal belongings.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetTempleSpecs(Guid? ownerId)
    {
        return
        [
            new RoomSpec(
                "Sanctuary",
                "A serene hall for prayer and worship.",
                0,
                20,
                [
                    new PropSpec(
                        "Altar",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Altar",
                                Description = "A sacred altar for prayer.",
                                WorkstationType = WorkstationType.Prayer,
                            }
                    ),
                    new PropSpec(
                        "Pew",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Pew",
                                Description = "A wooden pew.",
                            }
                    ),
                    new PropSpec(
                        "Pew",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Pew",
                                Description = "A wooden pew.",
                            }
                    ),
                    new PropSpec(
                        "Pew",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Pew",
                                Description = "A wooden pew.",
                            }
                    ),
                    new PropSpec(
                        "Altar",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Altar",
                                Description = "A second altar for a visiting cleric.",
                                WorkstationType = WorkstationType.Prayer,
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Quarters",
                "Living quarters for the temple keeper.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A simple bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A chest for personal belongings.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetTavernSpecs(Guid? ownerId)
    {
        return
        [
            new RoomSpec(
                "Common Room",
                "A lively room filled with the sounds of eating and drinking.",
                0,
                15,
                [
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A wooden chair.",
                            }
                    ),
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A wooden chair.",
                            }
                    ),
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A wooden chair.",
                            }
                    ),
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A wooden chair.",
                            }
                    ),
                    new PropSpec(
                        "Fireplace",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Fireplace",
                                Description = "A large fireplace used for cooking.",
                                WorkstationType = WorkstationType.Cooking,
                            }
                    ),
                    new PropSpec(
                        "Bar Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bar Counter",
                                Description = "A long counter for serving drinks.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Owner's Quarters",
                "The owner's private living space.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "The owner's bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A chest for the owner's belongings.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetInnSpecs(Guid? ownerId)
    {
        var guestRooms = InnGuestRoomDirections
            .Select(direction => new RoomSpec(
                $"{direction} Guest Room",
                "A small but tidy guest room.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A comfortable bed for guests.",
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A chest for guest belongings.",
                            }
                    ),
                ]
            ))
            .ToArray();

        return
        [
            new RoomSpec(
                "Lobby",
                "A welcoming lobby with a check-in counter.",
                0,
                15,
                [
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A cushioned chair.",
                            }
                    ),
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A cushioned chair.",
                            }
                    ),
                    new PropSpec(
                        "Innkeeper's Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Innkeeper's Counter",
                                Description = "A counter for booking rooms.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
                            }
                    ),
                ]
            ),
            .. guestRooms,
            new RoomSpec(
                "Owner's Quarters",
                "The innkeeper's private living space.",
                2,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "The innkeeper's bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A chest for personal belongings.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetGuildHallSpecs(Guid? ownerId, IReadOnlyList<Guid> memberIds)
    {
        var memberRooms = memberIds
            .Select(
                (memberId, i) =>
                    new RoomSpec(
                        i == 0 ? "Guild Master's Chamber" : $"Member Room {i}",
                        i == 0
                            ? "Private quarters for the guild master."
                            : "A private room for a guild member.",
                        1,
                        1,
                        [
                            new PropSpec(
                                "Bed",
                                (id, worldId) =>
                                    new Bed
                                    {
                                        LocationId = id,
                                        WorldId = worldId,
                                        Name = "Bed",
                                        Description = "A modest bed.",
                                        AssignedCreatureId = memberId,
                                    }
                            ),
                            new PropSpec(
                                "Chest",
                                (id, worldId) =>
                                    new Container
                                    {
                                        LocationId = id,
                                        WorldId = worldId,
                                        Name = "Chest",
                                        Description = "A chest for personal belongings.",
                                    }
                            ),
                        ]
                    )
            )
            .ToArray();

        return
        [
            new RoomSpec(
                "Hall",
                "A large hall where guild members gather.",
                0,
                15,
                [
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A sturdy chair.",
                            }
                    ),
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A sturdy chair.",
                            }
                    ),
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A sturdy chair.",
                            }
                    ),
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A sturdy chair.",
                            }
                    ),
                    new PropSpec(
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A counter for guild business.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
                            }
                    ),
                ]
            ),
            .. memberRooms,
        ];
    }

    private static RoomSpec[] GetHouseSpecs(IReadOnlyList<IReadOnlyList<Guid>> bedroomGroups)
    {
        var bedrooms = bedroomGroups
            .Select(
                (occupantIds, i) =>
                    new RoomSpec(
                        $"Bedroom {i + 1}",
                        "A modest bedroom.",
                        1,
                        occupantIds.Count,
                        [
                            .. occupantIds.Select(occupantId => new PropSpec(
                                "Bed",
                                (id, worldId) =>
                                    new Bed
                                    {
                                        LocationId = id,
                                        WorldId = worldId,
                                        Name = "Bed",
                                        Description = "A modest bed.",
                                        AssignedCreatureId = occupantId,
                                    }
                            )),
                            new PropSpec(
                                "Chest",
                                (id, worldId) =>
                                    new Container
                                    {
                                        LocationId = id,
                                        WorldId = worldId,
                                        Name = "Chest",
                                        Description = "A chest for personal belongings.",
                                    }
                            ),
                        ]
                    )
            )
            .ToArray();

        return
        [
            new RoomSpec(
                "Living Room",
                "A simple living room.",
                0,
                6,
                [
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A worn chair.",
                            }
                    ),
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A worn chair.",
                            }
                    ),
                ]
            ),
            .. bedrooms,
        ];
    }

    private static RoomSpec[] GetStableSpecs(Guid? ownerId)
    {
        return
        [
            new RoomSpec(
                "Stable",
                "A stable housing horses and supplies.",
                0,
                6,
                [
                    new PropSpec(
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A counter for selling horses and supplies.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Barrel",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Barrel",
                                Description = "A barrel of feed.",
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Living Quarters",
                "Simple living quarters for the stable hand.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A simple bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetBarracksSpecs(IReadOnlyList<Guid> memberIds)
    {
        var officerId = memberIds.Count > 0 ? memberIds[0] : (Guid?)null;
        var dormitoryBeds = memberIds
            .Skip(1)
            .Select(guardId => new PropSpec(
                "Bed",
                (id, worldId) =>
                    new Bed
                    {
                        LocationId = id,
                        WorldId = worldId,
                        Name = "Bed",
                        Description = "A simple bunk.",
                        AssignedCreatureId = guardId,
                    }
            ))
            .ToArray();

        return
        [
            new RoomSpec(
                "Drill Hall",
                "A hall lined with weapon racks and training gear.",
                0,
                15,
                [
                    new PropSpec(
                        "Weapon Rack",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Weapon Rack",
                                Description = "A rack holding practice weapons.",
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Officer's Quarters",
                "A spartan private room for the commanding officer.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A simple cot.",
                                AssignedCreatureId = officerId,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A footlocker for personal effects.",
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Barracks Dormitory",
                "Rows of bunks for the guards on duty.",
                1,
                20,
                [
                    .. dormitoryBeds,
                    new PropSpec(
                        "Footlocker",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Footlocker",
                                Description = "A shared footlocker for personal effects.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetCastleSpecs(Guid? ownerId)
    {
        return
        [
            new RoomSpec(
                "Great Hall",
                "A grand hall fit for a lord.",
                0,
                15,
                [
                    new PropSpec(
                        "Throne",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Throne",
                                Description = "An imposing throne.",
                            }
                    ),
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A chair for guests.",
                            }
                    ),
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A chair for guests.",
                            }
                    ),
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A chair for guests.",
                            }
                    ),
                ]
            ),
            new RoomSpec(
                "Royal Chambers",
                "Lavish private chambers for the lord.",
                1,
                1,
                [
                    new PropSpec(
                        "Bed",
                        (id, worldId) =>
                            new Bed
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Bed",
                                Description = "A grand four-poster bed.",
                                AssignedCreatureId = ownerId,
                            }
                    ),
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "An ornate chest.",
                            }
                    ),
                ]
            ),
        ];
    }

    private static RoomSpec[] GetJailSpecs()
    {
        return
        [
            new RoomSpec(
                "Guard Station",
                "A room where guards keep watch.",
                0,
                4,
                [
                    new PropSpec(
                        "Chair",
                        (id, worldId) =>
                            new Seat
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chair",
                                Description = "A chair for the guard on duty.",
                            }
                    ),
                ]
            ),
            new RoomSpec(
                JailRoomNames.Cells,
                "A row of dark, damp cells.",
                1,
                4,
                [
                    new PropSpec(
                        "Chest",
                        (id, worldId) =>
                            new Container
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Chest",
                                Description = "A chest for confiscated belongings.",
                            }
                    ),
                ]
            ),
        ];
    }
}
