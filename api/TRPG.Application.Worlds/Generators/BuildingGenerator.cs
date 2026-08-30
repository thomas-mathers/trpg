using TRPG.Application.Buildings;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Generators;

internal record BuildingGeneratorInput(Location ExteriorLocation, BuildingSpec Spec)
{
    public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    public required string Name { get; init; }
    public Guid? OwnerCreatureId { get; init; }
}

internal record BuildingGeneratorResult(
    Building Building,
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Prop> Props,
    IReadOnlyList<Location> Locations,
    IReadOnlyList<LocationConnector> LocationConnectors,
    DoorConnector FrontDoor,
    IReadOnlyList<Item> KeyItems,
    IReadOnlyList<DoorConnectorKey> DoorConnectorKeys,
    IReadOnlyList<DoorConnector> InteriorDoors
);

public class BuildingGenerator
{
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

    internal BuildingGeneratorResult Generate(BuildingGeneratorInput input)
    {
        var worldId = input.ExteriorLocation.WorldId;
        var building = new Building
        {
            ExteriorLocationId = input.ExteriorLocation.Id,
            BuildingType = input.Spec.Type,
            Name = input.Name,
            WorldId = worldId,
        };

        var locations = new List<Location>();
        var locationConnectors = new List<LocationConnector>();
        var rooms = input
            .Spec.Rooms.Select(s =>
            {
                var roomId = Guid.NewGuid();
                var location = LocationGenerator.Generate(
                    worldId,
                    input.ExteriorLocation.StateId,
                    input.ExteriorLocation.CityId,
                    input.ExteriorLocation.DistrictId,
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
                    WorldId = worldId,
                };
            })
            .ToArray();

        var props = input
            .Spec.Rooms.Zip(rooms)
            .SelectMany(pair =>
                pair.First.Props.Select(p => p.Factory(pair.Second.LocationId, worldId))
            )
            .ToList();

        foreach (var prop in props)
        {
            prop.OwnerCreatureId = input.OwnerCreatureId;
        }

        var roomsByFloor = rooms.GroupBy(r => r.FloorNumber).OrderBy(g => g.Key).ToArray();
        var allRooms = new List<Room>(rooms);
        var interiorDoors = new List<DoorConnector>();
        var keyItems = new List<Item>();
        var doorConnectorKeys = new List<DoorConnectorKey>();

        // The Innkeeper's Counter is the only Trade workstation an Inn ever generates.
        var innkeepersCounterId =
            input.Spec.Type == BuildingType.Inn
                ? props
                    .OfType<Workstation>()
                    .First(w => w.WorkstationType == WorkstationType.Trade)
                    .Id
                : (Guid?)null;

        // Multi-room floors route through a synthesized hallway instead of an arbitrary "first" room.
        var connectionPointByFloor = new Room[roomsByFloor.Length];
        for (var i = 0; i < roomsByFloor.Length; i++)
        {
            var floorRooms = roomsByFloor[i].ToArray();
            if (floorRooms.Length == 1)
            {
                connectionPointByFloor[i] = floorRooms[0];
                continue;
            }

            var hallwayId = Guid.NewGuid();
            var hallwayLocation = LocationGenerator.Generate(
                worldId,
                input.ExteriorLocation.StateId,
                input.ExteriorLocation.CityId,
                input.ExteriorLocation.DistrictId,
                hallwayId
            );
            locations.Add(hallwayLocation);

            var hallway = new Room
            {
                Id = hallwayId,
                BuildingId = building.Id,
                LocationId = hallwayLocation.Id,
                Capacity = 4,
                Description = "A connecting hallway.",
                FloorNumber = floorRooms[0].FloorNumber,
                Name = "Hallway",
                WorldId = worldId,
            };
            allRooms.Add(hallway);
            connectionPointByFloor[i] = hallway;

            foreach (var room in floorRooms)
            {
                var toRoomConnector = new LocationConnector
                {
                    OriginLocationId = hallway.LocationId,
                    Name = room.Name,
                    Description = $"The way to {room.Name}.",
                    DestinationLocationId = room.LocationId,
                    DestinationLabel = room.Name,
                    WorldId = worldId,
                };
                locationConnectors.Add(toRoomConnector);
                locationConnectors.Add(
                    new LocationConnector
                    {
                        OriginLocationId = room.LocationId,
                        Name = "Hallway",
                        Description = "The way back to the hallway.",
                        DestinationLocationId = hallway.LocationId,
                        DestinationLabel = hallway.Name,
                        WorldId = worldId,
                    }
                );

                if (input.Spec.Type != BuildingType.Inn)
                {
                    continue;
                }

                var roomDoor = new DoorConnector
                {
                    ConnectorId = toRoomConnector.Id,
                    IsLocked = true,
                    WorldId = worldId,
                };
                interiorDoors.Add(roomDoor);

                var roomKey = new Key
                {
                    WorldId = worldId,
                    Name = $"Key to {room.Name}",
                    Description = $"A key that unlocks the {room.Name} in {building.Name}.",
                    Quantity = 1,
                    Ownership = new ItemOwnership
                    {
                        OwnerId = innkeepersCounterId!.Value,
                        OwnerType = OwnerType.Workstation,
                    },
                };
                keyItems.Add(roomKey);
                doorConnectorKeys.Add(
                    new DoorConnectorKey
                    {
                        ItemId = roomKey.Id,
                        DoorConnectorId = roomDoor.Id,
                        WorldId = worldId,
                    }
                );
            }
        }

        for (var i = 0; i < connectionPointByFloor.Length - 1; i++)
        {
            var roomAbove = connectionPointByFloor[i + 1];
            var roomBelow = connectionPointByFloor[i];

            locationConnectors.Add(
                new LocationConnector
                {
                    OriginLocationId = roomBelow.LocationId,
                    Name = "Staircase",
                    Description = "A staircase leading up.",
                    DestinationLocationId = roomAbove.LocationId,
                    DestinationLabel = roomAbove.Name,
                    WorldId = worldId,
                }
            );

            var downConnector = new LocationConnector
            {
                OriginLocationId = roomAbove.LocationId,
                Name = "Staircase",
                Description = "A staircase leading down.",
                DestinationLocationId = roomBelow.LocationId,
                DestinationLabel = roomBelow.Name,
                WorldId = worldId,
            };
            locationConnectors.Add(downConnector);

            if (input.Spec.Type == BuildingType.Jail && roomAbove.Name == JailRoomNames.Cells)
            {
                var cellsDoor = new DoorConnector
                {
                    ConnectorId = downConnector.Id,
                    IsLocked = true,
                    WorldId = worldId,
                };
                interiorDoors.Add(cellsDoor);

                foreach (var guardId in input.MemberIds)
                {
                    var cellKeyItem = new Key
                    {
                        WorldId = worldId,
                        Name = "Cell Key",
                        Description = $"A key that unlocks the cells in {building.Name}.",
                        Quantity = 1,
                        Ownership = new ItemOwnership
                        {
                            OwnerId = guardId,
                            OwnerType = OwnerType.Creature,
                        },
                    };
                    keyItems.Add(cellKeyItem);
                    doorConnectorKeys.Add(
                        new DoorConnectorKey
                        {
                            ItemId = cellKeyItem.Id,
                            DoorConnectorId = cellsDoor.Id,
                            WorldId = worldId,
                        }
                    );
                }
            }
        }

        var entranceRoom = connectionPointByFloor[0];
        var exitConnector = new LocationConnector
        {
            OriginLocationId = entranceRoom.LocationId,
            Name = "Front Door",
            Description = "The door leading outside.",
            DestinationLocationId = input.ExteriorLocation.Id,
            DestinationLabel = "Outside",
            WorldId = worldId,
        };
        var entryConnector = new LocationConnector
        {
            OriginLocationId = input.ExteriorLocation.Id,
            Name = "Front Door",
            Description = $"The door leading into {building.Name}.",
            DestinationLocationId = entranceRoom.LocationId,
            DestinationLabel = building.Name,
            WorldId = worldId,
        };
        locationConnectors.Add(exitConnector);
        locationConnectors.Add(entryConnector);

        // Only the entry side is lockable — someone already inside can always leave.
        var frontDoor = new DoorConnector
        {
            ConnectorId = entryConnector.Id,
            IsLocked = input.Spec.IsLockable,
            WorldId = worldId,
        };

        if (input.Spec.IsLockable)
        {
            foreach (var residentId in input.MemberIds)
            {
                var keyItem = new Key
                {
                    WorldId = worldId,
                    Name = $"Key to {building.Name}",
                    Description = $"A key that unlocks {building.Name}.",
                    Quantity = 1,
                    Ownership = new ItemOwnership
                    {
                        OwnerId = residentId,
                        OwnerType = OwnerType.Creature,
                    },
                };
                keyItems.Add(keyItem);
                doorConnectorKeys.Add(
                    new DoorConnectorKey
                    {
                        ItemId = keyItem.Id,
                        DoorConnectorId = frontDoor.Id,
                        WorldId = worldId,
                    }
                );
            }
        }

        return new BuildingGeneratorResult(
            building,
            allRooms,
            props,
            locations,
            locationConnectors,
            frontDoor,
            keyItems,
            doorConnectorKeys,
            interiorDoors
        );
    }
}
