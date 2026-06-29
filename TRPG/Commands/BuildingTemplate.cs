using TRPG.Models;

namespace TRPG.Commands;

internal static class BuildingTemplate {
    public static (IReadOnlyList<Room> Rooms, IReadOnlyList<Prop> Props) Create(
        BuildingType buildingType, Guid buildingId, Guid ownerId) {
        var specs = GetSpecs(buildingType, ownerId);
        var rooms = specs.Select(s => new Room {
            BuildingId = buildingId,
            Description = s.Description,
            FloorNumber = s.FloorNumber,
            Name = s.Name
        }).ToArray();

        var props = specs.Zip(rooms)
            .SelectMany(pair => pair.First.Props.Select(p => p.Factory(pair.Second.Id)))
            .ToArray();

        var roomsByFloor = rooms.GroupBy(r => r.FloorNumber).OrderBy(g => g.Key).ToArray();
        for (var i = 0; i < roomsByFloor.Length - 1; i++) {
            var roomAbove = roomsByFloor[i + 1].First();
            var roomBelow = roomsByFloor[i].First();
            props = [
                ..props,
                new RoomConnector {
                    RoomId = roomBelow.Id, Name = "Staircase", Description = "A staircase leading up.",
                    DestinationRoomId = roomAbove.Id
                },
                new RoomConnector {
                    RoomId = roomAbove.Id, Name = "Staircase", Description = "A staircase leading down.",
                    DestinationRoomId = roomBelow.Id
                }
            ];
        }

        return (rooms, props);
    }

    private static RoomSpec[] GetSpecs(BuildingType buildingType, Guid ownerId) {
        return buildingType switch {
            BuildingType.Blacksmith => [
                new RoomSpec("Workshop", "A working forge with tools and equipment for smithing.", 0, [
                    new PropSpec("Forge",
                        id => new Workstation {
                            RoomId = id, Name = "Forge", Description = "A roaring forge for shaping metal.",
                            WorkstationType = WorkstationType.Weaponsmithing
                        }),
                    new PropSpec("Anvil",
                        id => new Workstation {
                            RoomId = id, Name = "Anvil", Description = "A heavy anvil for hammering armour.",
                            WorkstationType = WorkstationType.Armorsmithing
                        }),
                    new PropSpec("Counter",
                        id => new Workstation {
                            RoomId = id, Name = "Counter", Description = "A wooden counter for trading.",
                            WorkstationType = WorkstationType.Trade, AssignedPersonId = ownerId
                        })
                ]),
                new RoomSpec("Living Quarters", "Simple living quarters above the shop.", 1, [
                    new PropSpec("Bed",
                        id => new Bed
                            { RoomId = id, Name = "Bed", Description = "A modest bed.", AssignedPersonId = ownerId }),
                    new PropSpec("Chest",
                        id => new Container
                            { RoomId = id, Name = "Chest", Description = "A sturdy chest for personal belongings." })
                ])
            ],
            BuildingType.Apothecary => [
                new RoomSpec("Shop", "A shop filled with the smell of herbs and potions.", 0, [
                    new PropSpec("Alchemy Table",
                        id => new Workstation {
                            RoomId = id, Name = "Alchemy Table",
                            Description = "A table covered in alchemical equipment.",
                            WorkstationType = WorkstationType.Alchemy
                        }),
                    new PropSpec("Counter",
                        id => new Workstation {
                            RoomId = id, Name = "Counter", Description = "A counter for selling remedies.",
                            WorkstationType = WorkstationType.Trade, AssignedPersonId = ownerId
                        })
                ]),
                new RoomSpec("Living Quarters", "Modest living quarters above the apothecary.", 1, [
                    new PropSpec("Bed",
                        id => new Bed
                            { RoomId = id, Name = "Bed", Description = "A modest bed.", AssignedPersonId = ownerId }),
                    new PropSpec("Chest",
                        id => new Container
                            { RoomId = id, Name = "Chest", Description = "A chest for personal belongings." })
                ])
            ],
            BuildingType.Bakery => [
                new RoomSpec("Bakery", "A warm room smelling of fresh bread.", 0, [
                    new PropSpec("Oven",
                        id => new Workstation {
                            RoomId = id, Name = "Oven", Description = "A large stone oven for baking.",
                            WorkstationType = WorkstationType.Cooking
                        }),
                    new PropSpec("Counter",
                        id => new Workstation {
                            RoomId = id, Name = "Counter", Description = "A counter for selling baked goods.",
                            WorkstationType = WorkstationType.Trade, AssignedPersonId = ownerId
                        })
                ]),
                new RoomSpec("Living Quarters", "Living quarters above the bakery.", 1, [
                    new PropSpec("Bed",
                        id => new Bed
                            { RoomId = id, Name = "Bed", Description = "A modest bed.", AssignedPersonId = ownerId }),
                    new PropSpec("Chest",
                        id => new Container
                            { RoomId = id, Name = "Chest", Description = "A chest for personal belongings." })
                ])
            ],
            BuildingType.ArcaneShop => [
                new RoomSpec("Shop", "A dimly lit shop filled with arcane curiosities.", 0, [
                    new PropSpec("Enchanting Table",
                        id => new Workstation {
                            RoomId = id, Name = "Enchanting Table",
                            Description = "A table humming with magical energy.",
                            WorkstationType = WorkstationType.Enchanting
                        }),
                    new PropSpec("Counter",
                        id => new Workstation {
                            RoomId = id, Name = "Counter", Description = "A counter for selling arcane wares.",
                            WorkstationType = WorkstationType.Trade, AssignedPersonId = ownerId
                        })
                ]),
                new RoomSpec("Living Quarters", "Living quarters above the arcane shop.", 1, [
                    new PropSpec("Bed",
                        id => new Bed
                            { RoomId = id, Name = "Bed", Description = "A modest bed.", AssignedPersonId = ownerId }),
                    new PropSpec("Chest",
                        id => new Container
                            { RoomId = id, Name = "Chest", Description = "A chest for personal belongings." })
                ])
            ],
            BuildingType.GeneralGoods => [
                new RoomSpec("Shop", "A well-stocked shop with a wide variety of goods.", 0, [
                    new PropSpec("Counter",
                        id => new Workstation {
                            RoomId = id, Name = "Counter", Description = "A counter for general trading.",
                            WorkstationType = WorkstationType.Trade, AssignedPersonId = ownerId
                        }),
                    new PropSpec("Crate",
                        id => new Container { RoomId = id, Name = "Crate", Description = "A large crate of goods." }),
                    new PropSpec("Barrel",
                        id => new Container { RoomId = id, Name = "Barrel", Description = "A barrel of supplies." })
                ]),
                new RoomSpec("Living Quarters", "Living quarters behind the shop.", 1, [
                    new PropSpec("Bed",
                        id => new Bed
                            { RoomId = id, Name = "Bed", Description = "A modest bed.", AssignedPersonId = ownerId }),
                    new PropSpec("Chest",
                        id => new Container
                            { RoomId = id, Name = "Chest", Description = "A chest for personal belongings." })
                ])
            ],
            BuildingType.Library => [
                new RoomSpec("Reading Room", "A quiet room lined with shelves of books.", 0, [
                    new PropSpec("Bookcase",
                        id => new Workstation {
                            RoomId = id, Name = "Bookcase", Description = "A tall bookcase filled with tomes.",
                            WorkstationType = WorkstationType.Reading
                        }),
                    new PropSpec("Bookcase",
                        id => new Workstation {
                            RoomId = id, Name = "Bookcase", Description = "A tall bookcase filled with manuscripts.",
                            WorkstationType = WorkstationType.Reading
                        }),
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A comfortable reading chair." }),
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A comfortable reading chair." }),
                    new PropSpec("Counter",
                        id => new Workstation {
                            RoomId = id, Name = "Counter", Description = "A counter for lending books.",
                            WorkstationType = WorkstationType.Trade, AssignedPersonId = ownerId
                        })
                ]),
                new RoomSpec("Study", "A private study for the librarian.", 1, [
                    new PropSpec("Bed",
                        id => new Bed
                            { RoomId = id, Name = "Bed", Description = "A modest bed.", AssignedPersonId = ownerId }),
                    new PropSpec("Bookcase",
                        id => new Workstation {
                            RoomId = id, Name = "Bookcase", Description = "A personal bookcase.",
                            WorkstationType = WorkstationType.Reading
                        }),
                    new PropSpec("Chest",
                        id => new Container
                            { RoomId = id, Name = "Chest", Description = "A chest for personal belongings." })
                ])
            ],
            BuildingType.Temple => [
                new RoomSpec("Sanctuary", "A serene hall for prayer and worship.", 0, [
                    new PropSpec("Altar",
                        id => new Workstation {
                            RoomId = id, Name = "Altar", Description = "A sacred altar for prayer.",
                            WorkstationType = WorkstationType.Prayer
                        }),
                    new PropSpec("Pew", id => new Seat { RoomId = id, Name = "Pew", Description = "A wooden pew." }),
                    new PropSpec("Pew", id => new Seat { RoomId = id, Name = "Pew", Description = "A wooden pew." }),
                    new PropSpec("Pew", id => new Seat { RoomId = id, Name = "Pew", Description = "A wooden pew." }),
                    new PropSpec("Counter",
                        id => new Workstation {
                            RoomId = id, Name = "Counter", Description = "A counter for donations and offerings.",
                            WorkstationType = WorkstationType.Trade, AssignedPersonId = ownerId
                        })
                ]),
                new RoomSpec("Quarters", "Living quarters for the temple keeper.", 1, [
                    new PropSpec("Bed",
                        id => new Bed
                            { RoomId = id, Name = "Bed", Description = "A simple bed.", AssignedPersonId = ownerId }),
                    new PropSpec("Chest",
                        id => new Container
                            { RoomId = id, Name = "Chest", Description = "A chest for personal belongings." })
                ])
            ],
            BuildingType.Tavern => [
                new RoomSpec("Common Room", "A lively room filled with the sounds of eating and drinking.", 0, [
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A wooden chair." }),
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A wooden chair." }),
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A wooden chair." }),
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A wooden chair." }),
                    new PropSpec("Fireplace",
                        id => new Workstation {
                            RoomId = id, Name = "Fireplace", Description = "A large fireplace used for cooking.",
                            WorkstationType = WorkstationType.Cooking
                        }),
                    new PropSpec("Bar Counter",
                        id => new Workstation {
                            RoomId = id, Name = "Bar Counter", Description = "A long counter for serving drinks.",
                            WorkstationType = WorkstationType.Trade, AssignedPersonId = ownerId
                        })
                ]),
                new RoomSpec("Guest Room 1", "A small but tidy guest room.", 1, [
                    new PropSpec("Bed",
                        id => new Bed { RoomId = id, Name = "Bed", Description = "A comfortable bed for guests." }),
                    new PropSpec("Chest",
                        id => new Container
                            { RoomId = id, Name = "Chest", Description = "A chest for guest belongings." })
                ]),
                new RoomSpec("Guest Room 2", "A small but tidy guest room.", 1, [
                    new PropSpec("Bed",
                        id => new Bed { RoomId = id, Name = "Bed", Description = "A comfortable bed for guests." }),
                    new PropSpec("Chest",
                        id => new Container
                            { RoomId = id, Name = "Chest", Description = "A chest for guest belongings." })
                ]),
                new RoomSpec("Guest Room 3", "A small but tidy guest room.", 1, [
                    new PropSpec("Bed",
                        id => new Bed { RoomId = id, Name = "Bed", Description = "A comfortable bed for guests." }),
                    new PropSpec("Chest",
                        id => new Container
                            { RoomId = id, Name = "Chest", Description = "A chest for guest belongings." })
                ]),
                new RoomSpec("Owner's Quarters", "The owner's private living space.", 2, [
                    new PropSpec("Bed",
                        id => new Bed {
                            RoomId = id, Name = "Bed", Description = "The owner's bed.", AssignedPersonId = ownerId
                        }),
                    new PropSpec("Chest",
                        id => new Container
                            { RoomId = id, Name = "Chest", Description = "A chest for the owner's belongings." })
                ])
            ],
            BuildingType.GuildHall => [
                new RoomSpec("Hall", "A large hall where guild members gather.", 0, [
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A sturdy chair." }),
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A sturdy chair." }),
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A sturdy chair." }),
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A sturdy chair." }),
                    new PropSpec("Counter",
                        id => new Workstation {
                            RoomId = id, Name = "Counter", Description = "A counter for guild business.",
                            WorkstationType = WorkstationType.Trade, AssignedPersonId = ownerId
                        })
                ]),
                new RoomSpec("Office", "A private office for the guild master.", 1, [
                    new PropSpec("Bed",
                        id => new Bed {
                            RoomId = id, Name = "Bed", Description = "The guild master's bed.",
                            AssignedPersonId = ownerId
                        }),
                    new PropSpec("Desk",
                        id => new Workstation {
                            RoomId = id, Name = "Desk", Description = "A desk covered in contracts and ledgers.",
                            WorkstationType = WorkstationType.Reading
                        }),
                    new PropSpec("Chest",
                        id => new Container { RoomId = id, Name = "Chest", Description = "A chest for guild records." })
                ])
            ],
            BuildingType.House => [
                new RoomSpec("Living Room", "A simple living room.", 0, [
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A worn chair." }),
                    new PropSpec("Chair", id => new Seat { RoomId = id, Name = "Chair", Description = "A worn chair." })
                ]),
                new RoomSpec("Bedroom", "A modest bedroom.", 1, [
                    new PropSpec("Bed",
                        id => new Bed
                            { RoomId = id, Name = "Bed", Description = "A modest bed.", AssignedPersonId = ownerId }),
                    new PropSpec("Chest",
                        id => new Container
                            { RoomId = id, Name = "Chest", Description = "A chest for personal belongings." })
                ])
            ],
            BuildingType.Stable => [
                new RoomSpec("Stable", "A stable housing horses and supplies.", 0, [
                    new PropSpec("Counter",
                        id => new Workstation {
                            RoomId = id, Name = "Counter", Description = "A counter for selling horses and supplies.",
                            WorkstationType = WorkstationType.Trade, AssignedPersonId = ownerId
                        }),
                    new PropSpec("Barrel",
                        id => new Container { RoomId = id, Name = "Barrel", Description = "A barrel of feed." })
                ]),
                new RoomSpec("Living Quarters", "Simple living quarters for the stable hand.", 1, [
                    new PropSpec("Bed",
                        id => new Bed
                            { RoomId = id, Name = "Bed", Description = "A simple bed.", AssignedPersonId = ownerId })
                ])
            ],
            BuildingType.Castle => [
                new RoomSpec("Great Hall", "A grand hall fit for a lord.", 0, [
                    new PropSpec("Throne",
                        id => new Seat { RoomId = id, Name = "Throne", Description = "An imposing throne." }),
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A chair for guests." }),
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A chair for guests." }),
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A chair for guests." }),
                    new PropSpec("Counter",
                        id => new Workstation {
                            RoomId = id, Name = "Counter", Description = "A counter for receiving tribute and trade.",
                            WorkstationType = WorkstationType.Trade, AssignedPersonId = ownerId
                        })
                ]),
                new RoomSpec("Royal Chambers", "Lavish private chambers for the lord.", 1, [
                    new PropSpec("Bed",
                        id => new Bed {
                            RoomId = id, Name = "Bed", Description = "A grand four-poster bed.",
                            AssignedPersonId = ownerId
                        }),
                    new PropSpec("Chest",
                        id => new Container { RoomId = id, Name = "Chest", Description = "An ornate chest." })
                ])
            ],
            BuildingType.Jail => [
                new RoomSpec("Guard Station", "A room where guards keep watch.", 0, [
                    new PropSpec("Chair",
                        id => new Seat { RoomId = id, Name = "Chair", Description = "A chair for the guard on duty." })
                ]),
                new RoomSpec("Cells", "A row of dark, damp cells.", 1, [
                    new PropSpec("Chest",
                        id => new Container
                            { RoomId = id, Name = "Chest", Description = "A chest for confiscated belongings." })
                ])
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(buildingType), buildingType, null)
        };
    }

    internal record RoomSpec(string Name, string Description, int FloorNumber, PropSpec[] Props);

    internal record PropSpec(string Name, Func<Guid, Prop> Factory);
}