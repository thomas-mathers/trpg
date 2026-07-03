using TRPG.Models;

namespace TRPG.Generators;

internal record BuildingGeneratorInput(
    Guid StateId,
    Guid CityId,
    Guid DistrictId,
    Guid? OwnerId,
    BuildingType Type
) {
    public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    public string? Name { get; init; }
}

internal record BuildingGeneratorResult(
    Building Building,
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Prop> Props
);

internal class BuildingGenerator {
    internal static readonly IReadOnlyCollection<BuildingType> CityBuildingTypes = [
        BuildingType.ArcaneShop, BuildingType.Apothecary, BuildingType.Bakery,
        BuildingType.Blacksmith, BuildingType.Castle, BuildingType.GeneralGoods,
        BuildingType.GuildHall, BuildingType.House, BuildingType.Jail,
        BuildingType.Library, BuildingType.Stable, BuildingType.Tavern, BuildingType.Temple
    ];

    internal static readonly IReadOnlyCollection<BuildingType> DungeonBuildingTypes = [
        BuildingType.Cave, BuildingType.Crypt, BuildingType.Mine,
        BuildingType.Ruins, BuildingType.Tower
    ];

    internal static readonly Dictionary<BuildingType, string[]> Names = new() {
        [BuildingType.ArcaneShop] = [
            "The Mystic Tome", "The Wandering Eye", "The Silver Sigil", "The Hidden Grimoire",
            "The Arcane Emporium", "Enchanted Relics", "The Spellwright's Corner",
            "The Runic Cache", "The Veil & Vellum", "The Curious Curio"
        ],
        [BuildingType.House] = [
            "Aldric's House", "Brenna's Cottage", "The Old Stone House", "The Thatched Roof",
            "The Corner House", "The Narrow House", "The Crooked Chimney",
            "The Low House", "The Timber House", "The Hearthside Home",
            "Merrow's House", "Dagny's Cottage", "The Mossy Roof", "The Leaning Chimney",
            "The Weathered Cottage", "The Ivy House", "The Gabled House", "The Sunken Cottage",
            "The Whitewashed House", "The Shingled Cottage", "Osric's House", "The Turret House",
            "The Quiet Cottage", "The Sagging Roof", "The Half Timbered House", "The Willow Cottage",
            "The Chimney House", "The Shuttered Cottage", "The Steep Roof House", "The Garden Cottage"
        ],
        [BuildingType.Apothecary] = [
            "The Healing Hand", "The Green Flask", "The Herb Garden", "The Mortar & Pestle",
            "The Remedy Shop", "The Alchemist's Corner", "The Tincture & Tonic",
            "The Potion Cellar", "Yarrow & Rue", "The Physick Hall"
        ],
        [BuildingType.Bakery] = [
            "The Golden Loaf", "The Warm Hearth", "The Rising Crust", "The Kneaded Dough",
            "The Crumb & Crust", "The Ember Oven", "The Harvest Loaf",
            "The Sweet Roll", "The Grain Basket", "The Flour Mill"
        ],
        [BuildingType.Blacksmith] = [
            "The Iron Anvil", "The Rusty Hammer", "The Forge & Fire", "The Steel Works",
            "The Hammered Shield", "The Ember Forge", "The Ironmonger",
            "The Tempered Blade", "The Smoldering Coal", "The Striker's Anvil"
        ],
        [BuildingType.Castle] = [
            "The Iron Keep", "The Stone Fortress", "The High Bastion", "The Warden's Tower",
            "The Royal Seat", "The Grey Citadel", "The Rampart Keep",
            "The Duke's Stronghold", "The Iron Seat", "The Sunken Keep"
        ],
        [BuildingType.GeneralGoods] = [
            "The Trading Post", "The Market Stall", "The Supply Corner", "The Provisions Shop",
            "The Sundry Store", "The Peddler's Pack", "The Dry Goods",
            "The Common Wares", "The Merchant's Cache", "The Open Barrel"
        ],
        [BuildingType.GuildHall] = [
            "The Guild House", "The Order Hall", "The Brotherhood Hall", "The Common Charter",
            "The Sealed Compact", "The Meeting Lodge", "The Trade Moot",
            "The Charter House", "The Open Hall", "The Register Room"
        ],
        [BuildingType.Jail] = [
            "The Iron Gate", "The Stone Cell", "The Warden's Hold", "The Grim Cage",
            "The Cold Bar", "The Mute Dungeon", "The Debtor's Hold",
            "The Lock & Chain", "The Sober Room", "The Quiet Pit"
        ],
        [BuildingType.Library] = [
            "The Dusty Pages", "The Scholar's Retreat", "The Open Book", "The Chronicle Hall",
            "The Lending Stack", "The Ink & Parchment", "The Bound Collection",
            "The Reading Nook", "The Archive Hall", "The Scrivener's Den"
        ],
        [BuildingType.Stable] = [
            "The Wanderer's Rest", "The Iron Shoe", "The Haystack", "The Rider's Lodge",
            "The Trodden Path", "The Oat & Saddle", "The Hitching Post",
            "The Canter & Comb", "The Straw Bale", "The Horseman's Haven"
        ],
        [BuildingType.Tavern] = [
            "The Rusty Flagon", "The Wandering Bard", "The Hearth & Hound", "The Tipped Barrel",
            "The Crooked Stool", "The Salted Rim", "The Ember & Ale",
            "The Common Room", "The Stumbling Pilgrim", "The Last Drop"
        ],
        [BuildingType.Temple] = [
            "The Sacred Flame", "The Divine Light", "The Pilgrim's Shrine", "The Holy Hearth",
            "The Sanctum of Faith", "The Blessed Hall", "The Quiet Chapel",
            "The Offering Stone", "The Votive Lantern", "The Celestial Gate"
        ]
    };

    public BuildingGeneratorResult Generate(BuildingGeneratorInput input) {
        if (!CityBuildingTypes.Contains(input.Type))
            throw new InvalidOperationException(
                $"{input.Type} cannot be placed in a city.");

        var names = Names[input.Type];
        var building = new Building {
            StateId = input.StateId,
            CityId = input.CityId,
            DistrictId = input.DistrictId,
            BuildingType = input.Type,
            Name = input.Name ?? names[Random.Shared.Next(names.Length)]
        };
        var specs = GetSpecs(input.Type, input.OwnerId, input.MemberIds);

        var rooms = specs.Select(s => new Room {
            BuildingId = building.Id,
            Description = s.Description,
            FloorNumber = s.FloorNumber,
            Name = s.Name
        }).ToArray();

        var props = specs.Zip(rooms)
            .SelectMany(pair => pair.First.Props.Select(p => p.Factory(pair.Second.Id)))
            .ToList();

        var roomsByFloor = rooms.GroupBy(r => r.FloorNumber).OrderBy(g => g.Key).ToArray();

        for (var i = 0; i < roomsByFloor.Length - 1; i++) {
            var roomAbove = roomsByFloor[i + 1].First();
            var roomBelow = roomsByFloor[i].First();

            props.Add(new RoomConnector {
                RoomId = roomBelow.Id, Name = "Staircase", Description = "A staircase leading up.",
                DestinationRoomId = roomAbove.Id
            });

            props.Add(new RoomConnector {
                RoomId = roomAbove.Id, Name = "Staircase", Description = "A staircase leading down.",
                DestinationRoomId = roomBelow.Id
            });
        }

        var entranceRoom = rooms.First(r => r.FloorNumber == 0);
        props.Add(new RoomConnector {
            RoomId = entranceRoom.Id,
            Name = "Front Door",
            Description = "The door leading outside.",
            DestinationRoomId = null
        });

        return new BuildingGeneratorResult(building, rooms, props);
    }

    private static RoomSpec[] GetSpecs(BuildingType buildingType, Guid? ownerId, IReadOnlyList<Guid> memberIds) {
        return buildingType switch {
            BuildingType.House => GetHouseSpecs(ownerId),
            BuildingType.Tavern => GetTavernSpecs(ownerId),
            BuildingType.Blacksmith => GetBlacksmithSpecs(ownerId),
            BuildingType.Temple => GetTempleSpecs(ownerId),
            BuildingType.Library => GetLibrarySpecs(ownerId),
            BuildingType.GeneralGoods => GetGeneralGoodsSpecs(ownerId),
            BuildingType.Apothecary => GetApothecarySpecs(ownerId),
            BuildingType.Bakery => GetBakerySpecs(ownerId),
            BuildingType.Stable => GetStableSpecs(ownerId),
            BuildingType.ArcaneShop => GetArcaneShopSpecs(ownerId),
            BuildingType.GuildHall => GetGuildHallSpecs(ownerId, memberIds),
            BuildingType.Castle => GetCastleSpecs(ownerId),
            BuildingType.Jail => GetJailSpecs(),
            _ => []
        };
    }

    private static RoomSpec[] GetBlacksmithSpecs(Guid? ownerId) {
        return [
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
        ];
    }

    private static RoomSpec[] GetApothecarySpecs(Guid? ownerId) {
        return [
            new RoomSpec("Shop", "A shop filled with the smell of herbs and potions.", 0, [
                new PropSpec("Alchemy Table",
                    id => new Workstation {
                        RoomId = id, Name = "Alchemy Table", Description = "A table covered in alchemical equipment.",
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
        ];
    }

    private static RoomSpec[] GetBakerySpecs(Guid? ownerId) {
        return [
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
        ];
    }

    private static RoomSpec[] GetArcaneShopSpecs(Guid? ownerId) {
        return [
            new RoomSpec("Shop", "A dimly lit shop filled with arcane curiosities.", 0, [
                new PropSpec("Enchanting Table",
                    id => new Workstation {
                        RoomId = id, Name = "Enchanting Table", Description = "A table humming with magical energy.",
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
        ];
    }

    private static RoomSpec[] GetGeneralGoodsSpecs(Guid? ownerId) {
        return [
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
        ];
    }

    private static RoomSpec[] GetLibrarySpecs(Guid? ownerId) {
        return [
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
        ];
    }

    private static RoomSpec[] GetTempleSpecs(Guid? ownerId) {
        return [
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
        ];
    }

    private static RoomSpec[] GetTavernSpecs(Guid? ownerId) {
        return [
            new RoomSpec("Common Room", "A lively room filled with the sounds of eating and drinking.", 0, [
                new PropSpec("Chair", id => new Seat { RoomId = id, Name = "Chair", Description = "A wooden chair." }),
                new PropSpec("Chair", id => new Seat { RoomId = id, Name = "Chair", Description = "A wooden chair." }),
                new PropSpec("Chair", id => new Seat { RoomId = id, Name = "Chair", Description = "A wooden chair." }),
                new PropSpec("Chair", id => new Seat { RoomId = id, Name = "Chair", Description = "A wooden chair." }),
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
            new RoomSpec("North Guest Room", "A small but tidy guest room.", 1, [
                new PropSpec("Bed",
                    id => new Bed { RoomId = id, Name = "Bed", Description = "A comfortable bed for guests." }),
                new PropSpec("Chest",
                    id => new Container { RoomId = id, Name = "Chest", Description = "A chest for guest belongings." })
            ]),
            new RoomSpec("South Guest Room", "A small but tidy guest room.", 1, [
                new PropSpec("Bed",
                    id => new Bed { RoomId = id, Name = "Bed", Description = "A comfortable bed for guests." }),
                new PropSpec("Chest",
                    id => new Container { RoomId = id, Name = "Chest", Description = "A chest for guest belongings." })
            ]),
            new RoomSpec("East Guest Room", "A small but tidy guest room.", 1, [
                new PropSpec("Bed",
                    id => new Bed { RoomId = id, Name = "Bed", Description = "A comfortable bed for guests." }),
                new PropSpec("Chest",
                    id => new Container { RoomId = id, Name = "Chest", Description = "A chest for guest belongings." })
            ]),
            new RoomSpec("Owner's Quarters", "The owner's private living space.", 2, [
                new PropSpec("Bed",
                    id => new Bed
                        { RoomId = id, Name = "Bed", Description = "The owner's bed.", AssignedPersonId = ownerId }),
                new PropSpec("Chest",
                    id => new Container
                        { RoomId = id, Name = "Chest", Description = "A chest for the owner's belongings." })
            ])
        ];
    }

    private static RoomSpec[] GetGuildHallSpecs(Guid? ownerId, IReadOnlyList<Guid> memberIds) {
        var memberRooms = memberIds.Select((memberId, i) => new RoomSpec(
            i == 0 ? "Guild Master's Chamber" : $"Member Room {i}",
            i == 0 ? "Private quarters for the guild master." : "A private room for a guild member.",
            1,
            [
                new PropSpec("Bed",
                    id => new Bed { RoomId = id, Name = "Bed", Description = "A modest bed.", AssignedPersonId = memberId }),
                new PropSpec("Chest",
                    id => new Container { RoomId = id, Name = "Chest", Description = "A chest for personal belongings." })
            ]
        )).ToArray();

        return [
            new RoomSpec("Hall", "A large hall where guild members gather.", 0, [
                new PropSpec("Chair", id => new Seat { RoomId = id, Name = "Chair", Description = "A sturdy chair." }),
                new PropSpec("Chair", id => new Seat { RoomId = id, Name = "Chair", Description = "A sturdy chair." }),
                new PropSpec("Chair", id => new Seat { RoomId = id, Name = "Chair", Description = "A sturdy chair." }),
                new PropSpec("Chair", id => new Seat { RoomId = id, Name = "Chair", Description = "A sturdy chair." }),
                new PropSpec("Counter",
                    id => new Workstation {
                        RoomId = id, Name = "Counter", Description = "A counter for guild business.",
                        WorkstationType = WorkstationType.Trade, AssignedPersonId = ownerId
                    })
            ]),
            ..memberRooms
        ];
    }

    private static RoomSpec[] GetHouseSpecs(Guid? ownerId) {
        return [
            new RoomSpec("Living Room", "A simple living room.", 0, [
                new PropSpec("Chair", id => new Seat { RoomId = id, Name = "Chair", Description = "A worn chair." }),
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
        ];
    }

    private static RoomSpec[] GetStableSpecs(Guid? ownerId) {
        return [
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
        ];
    }

    private static RoomSpec[] GetCastleSpecs(Guid? ownerId) {
        return [
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
                        RoomId = id, Name = "Bed", Description = "A grand four-poster bed.", AssignedPersonId = ownerId
                    }),
                new PropSpec("Chest",
                    id => new Container { RoomId = id, Name = "Chest", Description = "An ornate chest." })
            ])
        ];
    }

    private static RoomSpec[] GetJailSpecs() {
        return [
            new RoomSpec("Guard Station", "A room where guards keep watch.", 0, [
                new PropSpec("Chair",
                    id => new Seat { RoomId = id, Name = "Chair", Description = "A chair for the guard on duty." })
            ]),
            new RoomSpec("Cells", "A row of dark, damp cells.", 1, [
                new PropSpec("Chest",
                    id => new Container
                        { RoomId = id, Name = "Chest", Description = "A chest for confiscated belongings." })
            ])
        ];
    }

    private record RoomSpec(string Name, string Description, int FloorNumber, PropSpec[] Props);

    private record PropSpec(string Name, Func<Guid, Prop> Factory);
}