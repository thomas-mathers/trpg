using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public record BuildingGeneratorInput(
    Guid StateId,
    Guid CityId,
    Guid DistrictId,
    Guid DistrictLocationId,
    Guid? OwnerId,
    BuildingType Type,
    Guid WorldId
)
{
    public IReadOnlyList<IReadOnlyList<Guid>>? BedroomGroups { get; init; }
    public bool IsLockable { get; init; }
    public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    public string? Name { get; init; }
}

public record BuildingGeneratorResult(
    Building Building,
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Prop> Props,
    IReadOnlyList<Location> Locations,
    LocationConnector FrontDoor
);

public class BuildingGenerator
{
    internal static readonly IReadOnlyCollection<BuildingType> CityBuildingTypes =
    [
        BuildingType.ArcaneShop,
        BuildingType.Apothecary,
        BuildingType.Bakery,
        BuildingType.Barracks,
        BuildingType.Blacksmith,
        BuildingType.Carpenter,
        BuildingType.Castle,
        BuildingType.GeneralGoods,
        BuildingType.GuildHall,
        BuildingType.House,
        BuildingType.Inn,
        BuildingType.Jail,
        BuildingType.Jeweler,
        BuildingType.Library,
        BuildingType.Stable,
        BuildingType.Tailor,
        BuildingType.Tavern,
        BuildingType.Temple,
    ];

    internal static readonly IReadOnlyCollection<BuildingType> DungeonBuildingTypes =
    [
        BuildingType.Cave,
        BuildingType.Crypt,
        BuildingType.Mine,
        BuildingType.Ruins,
        BuildingType.Tower,
    ];

    private static readonly string[] InnGuestRoomDirections = ["North", "South", "East", "West"];

    internal static readonly Dictionary<BuildingType, int> Popularity = new()
    {
        [BuildingType.Tavern] = 10,
        [BuildingType.Inn] = 8,
        [BuildingType.GuildHall] = 7,
        [BuildingType.GeneralGoods] = 6,
        [BuildingType.Bakery] = 6,
        [BuildingType.Temple] = 5,
        [BuildingType.Barracks] = 4,
        [BuildingType.Library] = 4,
        [BuildingType.ArcaneShop] = 3,
        [BuildingType.Apothecary] = 3,
        [BuildingType.Blacksmith] = 3,
        [BuildingType.House] = 3,
        [BuildingType.Stable] = 2,
        [BuildingType.Castle] = 2,
        [BuildingType.Tailor] = 3,
        [BuildingType.Carpenter] = 3,
        [BuildingType.Jeweler] = 3,
    };

    internal static readonly Dictionary<BuildingType, string[]> Names = new()
    {
        [BuildingType.ArcaneShop] =
        [
            "The Mystic Tome",
            "The Wandering Eye",
            "The Silver Sigil",
            "The Hidden Grimoire",
            "The Arcane Emporium",
            "Enchanted Relics",
            "The Spellwright's Corner",
            "The Runic Cache",
            "The Veil & Vellum",
            "The Curious Curio",
        ],
        [BuildingType.House] =
        [
            "Aldric's House",
            "Brenna's Cottage",
            "The Old Stone House",
            "The Thatched Roof",
            "The Corner House",
            "The Narrow House",
            "The Crooked Chimney",
            "The Low House",
            "The Timber House",
            "The Hearthside Home",
            "Merrow's House",
            "Dagny's Cottage",
            "The Mossy Roof",
            "The Leaning Chimney",
            "The Weathered Cottage",
            "The Ivy House",
            "The Gabled House",
            "The Sunken Cottage",
            "The Whitewashed House",
            "The Shingled Cottage",
            "Osric's House",
            "The Turret House",
            "The Quiet Cottage",
            "The Sagging Roof",
            "The Half Timbered House",
            "The Willow Cottage",
            "The Chimney House",
            "The Shuttered Cottage",
            "The Steep Roof House",
            "The Garden Cottage",
        ],
        [BuildingType.Apothecary] =
        [
            "The Healing Hand",
            "The Green Flask",
            "The Herb Garden",
            "The Mortar & Pestle",
            "The Remedy Shop",
            "The Alchemist's Corner",
            "The Tincture & Tonic",
            "The Potion Cellar",
            "Yarrow & Rue",
            "The Physick Hall",
        ],
        [BuildingType.Bakery] =
        [
            "The Golden Loaf",
            "The Warm Hearth",
            "The Rising Crust",
            "The Kneaded Dough",
            "The Crumb & Crust",
            "The Ember Oven",
            "The Harvest Loaf",
            "The Sweet Roll",
            "The Grain Basket",
            "The Flour Mill",
        ],
        [BuildingType.Barracks] =
        [
            "The Iron Watch",
            "The Garrison House",
            "The Muster Hall",
            "The Sentinel Post",
            "The Shield Wall",
            "The Standing Guard",
            "The Rampart Barracks",
            "The Drill Yard",
            "The Soldier's Rest",
            "The Guard Company",
        ],
        [BuildingType.Blacksmith] =
        [
            "The Iron Anvil",
            "The Rusty Hammer",
            "The Forge & Fire",
            "The Steel Works",
            "The Hammered Shield",
            "The Ember Forge",
            "The Ironmonger",
            "The Tempered Blade",
            "The Smoldering Coal",
            "The Striker's Anvil",
        ],
        [BuildingType.Castle] =
        [
            "The Iron Keep",
            "The Stone Fortress",
            "The High Bastion",
            "The Warden's Tower",
            "The Royal Seat",
            "The Grey Citadel",
            "The Rampart Keep",
            "The Duke's Stronghold",
            "The Iron Seat",
            "The Sunken Keep",
        ],
        [BuildingType.GeneralGoods] =
        [
            "The Trading Post",
            "The Market Stall",
            "The Supply Corner",
            "The Provisions Shop",
            "The Sundry Store",
            "The Peddler's Pack",
            "The Dry Goods",
            "The Common Wares",
            "The Merchant's Cache",
            "The Open Barrel",
        ],
        [BuildingType.GuildHall] =
        [
            "The Guild House",
            "The Order Hall",
            "The Brotherhood Hall",
            "The Common Charter",
            "The Sealed Compact",
            "The Meeting Lodge",
            "The Trade Moot",
            "The Charter House",
            "The Open Hall",
            "The Register Room",
        ],
        [BuildingType.Inn] =
        [
            "The Wanderer's Pillow",
            "The Weary Traveler",
            "The Lantern Rest",
            "The Sleepy Hollow Inn",
            "The Traveler's Respite",
            "The Golden Bedpost",
            "The Wayfarer's Lodge",
            "The Quiet Hearth Inn",
            "The Nightingale Inn",
            "The Drowsy Owl",
        ],
        [BuildingType.Jail] =
        [
            "The Iron Gate",
            "The Stone Cell",
            "The Warden's Hold",
            "The Grim Cage",
            "The Cold Bar",
            "The Mute Dungeon",
            "The Debtor's Hold",
            "The Lock & Chain",
            "The Sober Room",
            "The Quiet Pit",
        ],
        [BuildingType.Library] =
        [
            "The Dusty Pages",
            "The Scholar's Retreat",
            "The Open Book",
            "The Chronicle Hall",
            "The Lending Stack",
            "The Ink & Parchment",
            "The Bound Collection",
            "The Reading Nook",
            "The Archive Hall",
            "The Scrivener's Den",
        ],
        [BuildingType.Stable] =
        [
            "The Wanderer's Rest",
            "The Iron Shoe",
            "The Haystack",
            "The Rider's Lodge",
            "The Trodden Path",
            "The Oat & Saddle",
            "The Hitching Post",
            "The Canter & Comb",
            "The Straw Bale",
            "The Horseman's Haven",
        ],
        [BuildingType.Tavern] =
        [
            "The Rusty Flagon",
            "The Wandering Bard",
            "The Hearth & Hound",
            "The Tipped Barrel",
            "The Crooked Stool",
            "The Salted Rim",
            "The Ember & Ale",
            "The Common Room",
            "The Stumbling Pilgrim",
            "The Last Drop",
        ],
        [BuildingType.Temple] =
        [
            "The Sacred Flame",
            "The Divine Light",
            "The Pilgrim's Shrine",
            "The Holy Hearth",
            "The Sanctum of Faith",
            "The Blessed Hall",
            "The Quiet Chapel",
            "The Offering Stone",
            "The Votive Lantern",
            "The Celestial Gate",
        ],
        [BuildingType.Tailor] =
        [
            "The Needle & Thread",
            "The Silken Seam",
            "The Woven Bolt",
            "The Stitched Hem",
            "The Tailor's Cut",
            "The Fine Fabric",
            "The Buttoned Cuff",
            "The Spool & Shears",
            "The Draped Mannequin",
            "The Fitted Sleeve",
        ],
        [BuildingType.Carpenter] =
        [
            "The Sawdust Corner",
            "The Joined Plank",
            "The Whittled Post",
            "The Timber Yard",
            "The Carved Beam",
            "The Oak & Chisel",
            "The Fitted Joint",
            "The Wood Shaving",
            "The Sturdy Frame",
            "The Grain & Grove",
        ],
        [BuildingType.Jeweler] =
        [
            "The Cut Gem",
            "The Silver Setting",
            "The Polished Facet",
            "The Gilded Clasp",
            "The Precious Mount",
            "The Glinting Case",
            "The Faceted Stone",
            "The Fine Filigree",
            "The Jeweled Band",
            "The Bright Bezel",
        ],
    };

    public BuildingGeneratorResult Generate(BuildingGeneratorInput input)
    {
        if (!CityBuildingTypes.Contains(input.Type))
        {
            throw new InvalidOperationException($"{input.Type} cannot be placed in a city.");
        }

        var names = Names[input.Type];
        var building = new Building
        {
            StateId = input.StateId,
            CityId = input.CityId,
            DistrictId = input.DistrictId,
            BuildingType = input.Type,
            Name = input.Name ?? names[Random.Shared.Next(names.Length)],
            WorldId = input.WorldId,
        };
        var specs = GetSpecs(input.Type, input.OwnerId, input.MemberIds, input.BedroomGroups);

        var locations = new List<Location>();
        var rooms = specs
            .Select(s =>
            {
                var roomId = Guid.NewGuid();
                var location = LocationGenerator.Generate(
                    input.WorldId,
                    input.StateId,
                    input.CityId,
                    input.DistrictId,
                    roomId
                );
                locations.Add(location);
                return new Room
                {
                    Id = roomId,
                    BuildingId = building.Id,
                    LocationId = location.Id,
                    Capacity = s.Capacity,
                    Description = s.Description,
                    FloorNumber = s.FloorNumber,
                    Name = s.Name,
                    WorldId = input.WorldId,
                };
            })
            .ToArray();

        var props = specs
            .Zip(rooms)
            .SelectMany(pair =>
                pair.First.Props.Select(p => p.Factory(pair.Second.LocationId, input.WorldId))
            )
            .ToList();

        var roomsByFloor = rooms.GroupBy(r => r.FloorNumber).OrderBy(g => g.Key).ToArray();

        for (var i = 0; i < roomsByFloor.Length - 1; i++)
        {
            var roomAbove = roomsByFloor[i + 1].First();
            var roomBelow = roomsByFloor[i].First();

            props.Add(
                new LocationConnector
                {
                    LocationId = roomBelow.LocationId,
                    Name = "Staircase",
                    Description = "A staircase leading up.",
                    DestinationLocationId = roomAbove.LocationId,
                    DestinationLabel = roomAbove.Name,
                    WorldId = input.WorldId,
                }
            );

            props.Add(
                new LocationConnector
                {
                    LocationId = roomAbove.LocationId,
                    Name = "Staircase",
                    Description = "A staircase leading down.",
                    DestinationLocationId = roomBelow.LocationId,
                    DestinationLabel = roomBelow.Name,
                    WorldId = input.WorldId,
                }
            );
        }

        var entranceRoom = rooms.First(r => r.FloorNumber == 0);
        var frontDoor = new LocationConnector
        {
            LocationId = entranceRoom.LocationId,
            Name = "Front Door",
            Description = "The door leading outside.",
            DestinationLocationId = input.DistrictLocationId,
            DestinationLabel = "Outside",
            IsLocked = input.IsLockable,
            WorldId = input.WorldId,
        };
        props.Add(frontDoor);

        return new BuildingGeneratorResult(building, rooms, props, locations, frontDoor);
    }

    private static RoomSpec[] GetSpecs(
        BuildingType buildingType,
        Guid? ownerId,
        IReadOnlyList<Guid> memberIds,
        IReadOnlyList<IReadOnlyList<Guid>>? bedroomGroups
    )
    {
        return buildingType switch
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
            BuildingType.Barracks => GetBarracksSpecs(ownerId),
            BuildingType.ArcaneShop => GetArcaneShopSpecs(ownerId),
            BuildingType.GuildHall => GetGuildHallSpecs(ownerId, memberIds),
            BuildingType.Castle => GetCastleSpecs(ownerId),
            BuildingType.Jail => GetJailSpecs(),
            BuildingType.Tailor => GetTailorSpecs(ownerId),
            BuildingType.Carpenter => GetCarpenterSpecs(ownerId),
            BuildingType.Jeweler => GetJewelerSpecs(ownerId),
            _ => [],
        };
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
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A counter for donations and offerings.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
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

    private static RoomSpec[] GetBarracksSpecs(Guid? ownerId)
    {
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
                    new PropSpec(
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A counter for issuing orders and supplies.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
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
                                Description = "A footlocker for personal effects.",
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
                    new PropSpec(
                        "Counter",
                        (id, worldId) =>
                            new Workstation
                            {
                                LocationId = id,
                                WorldId = worldId,
                                Name = "Counter",
                                Description = "A counter for receiving tribute and trade.",
                                WorkstationType = WorkstationType.Trade,
                                AssignedCreatureId = ownerId,
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
                "Cells",
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

    private record RoomSpec(
        string Name,
        string Description,
        int FloorNumber,
        int Capacity,
        PropSpec[] Props
    );

    private record PropSpec(string Name, Func<Guid, Guid, Prop> Factory);
}
