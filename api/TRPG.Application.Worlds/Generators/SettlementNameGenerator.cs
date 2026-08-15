using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal static class SettlementNameGenerator
{
    private static readonly string[] RoadNouns =
    [
        "Road",
        "Trail",
        "Way",
        "Pass",
        "Route",
        "Path",
        "Trace",
        "Crossing",
    ];

    private static readonly string[] WildernessNouns =
    [
        "Wilds",
        "Reach",
        "Marches",
        "Hinterlands",
        "Woods",
        "Moors",
    ];

    private static readonly Dictionary<BuildingType, string[]> BuildingNouns = new()
    {
        [BuildingType.ArcaneShop] = ["Sigil", "Grimoire", "Rune", "Relic", "Ward", "Charm", "Tome"],
        [BuildingType.House] = ["House", "Cottage", "Hearth", "Dwelling", "Roof", "Home"],
        [BuildingType.Apothecary] = ["Flask", "Tonic", "Remedy", "Herb", "Elixir", "Vial"],
        [BuildingType.Bakery] = ["Loaf", "Oven", "Crust", "Hearth", "Grain", "Mill"],
        [BuildingType.Barracks] = ["Watch", "Garrison", "Muster", "Guard", "Sentinel", "Rampart"],
        [BuildingType.Blacksmith] = ["Anvil", "Forge", "Hammer", "Ember", "Iron", "Coal"],
        [BuildingType.Castle] = ["Keep", "Bastion", "Citadel", "Throne", "Rampart", "Seat"],
        [BuildingType.GeneralGoods] = ["Market", "Stall", "Wares", "Goods", "Pack", "Cache"],
        [BuildingType.GuildHall] = ["Hall", "Charter", "Lodge", "Compact", "Moot", "Chamber"],
        [BuildingType.Inn] = ["Rest", "Lodge", "Pillow", "Hearth", "Respite", "Bedpost"],
        [BuildingType.Jail] = ["Cell", "Gate", "Hold", "Cage", "Bar", "Pit"],
        [BuildingType.Library] = ["Pages", "Archive", "Tome", "Stack", "Scroll", "Chronicle"],
        [BuildingType.Stable] = ["Shoe", "Saddle", "Post", "Stall", "Haven", "Bale"],
        [BuildingType.Tavern] =
        [
            "Flagon",
            "Barrel",
            "Hearth",
            "Keg",
            "Mug",
            "Ale",
            "Cask",
            "Tankard",
        ],
        [BuildingType.Temple] = ["Flame", "Shrine", "Altar", "Sanctum", "Chapel", "Gate"],
    };

    private static readonly Dictionary<CreatureType, NamePool> Pools = new()
    {
        [CreatureType.Human] = new NamePool(
            [
                "Ash",
                "Raven",
                "Stone",
                "Kings",
                "Lark",
                "Bright",
                "River",
                "Oak",
                "Iron",
                "Wolf",
                "Hollow",
                "Thorn",
                "Grey",
                "Fair",
                "North",
                "West",
                "East",
                "South",
                "High",
                "Dark",
            ],
            [
                "ford",
                "crest",
                "bridge",
                "ley",
                "spur",
                "haven",
                "vale",
                "mere",
                "wick",
                "field",
                "moor",
                "hall",
                "borough",
                "worth",
                "gate",
                "hollow",
                "reach",
                "stead",
                "ridge",
                "brook",
            ]
        ),
        [CreatureType.Elf] = new NamePool(
            [
                "Silver",
                "Moon",
                "Star",
                "Dusk",
                "Ever",
                "Wyld",
                "Sun",
                "Wind",
                "Leaf",
                "Bright",
                "Whisper",
                "Shadow",
                "Dawn",
                "Mist",
                "Willow",
            ],
            [
                "hollow",
                "spire",
                "mere",
                "wood",
                "bloom",
                "vale",
                "shade",
                "song",
                "glen",
                "leaf",
                "fall",
                "reach",
                "haven",
                "grove",
                "light",
            ]
        ),
        [CreatureType.Dwarf] = new NamePool(
            [
                "Iron",
                "Stone",
                "Deep",
                "Grim",
                "Coal",
                "Granite",
                "Gold",
                "Silver",
                "Copper",
                "Ember",
                "Frost",
                "Steel",
                "Rune",
                "Dark",
                "Anvil",
            ],
            [
                "hold",
                "forge",
                "reach",
                "delve",
                "gard",
                "spire",
                "deep",
                "vault",
                "hall",
                "watch",
                "peak",
                "keep",
                "mine",
                "crag",
                "shield",
            ]
        ),
        [CreatureType.Orc] = new NamePool(
            [
                "Skarn",
                "Ghaz",
                "Grim",
                "Blood",
                "War",
                "Dread",
                "Iron",
                "Bone",
                "Black",
                "Death",
                "Rot",
                "Fang",
                "Skull",
                "Ash",
                "Rust",
            ],
            [
                "hold",
                "camp",
                "scar",
                "maw",
                "tusk",
                "fang",
                "gash",
                "pit",
                "reach",
                "hollow",
                "grip",
                "bane",
                "spire",
                "watch",
                "gate",
            ]
        ),
        [CreatureType.Halfling] = new NamePool(
            [
                "Apple",
                "Honey",
                "Meadow",
                "Thistle",
                "Hay",
                "Oak",
                "Berry",
                "Clover",
                "Fern",
                "Rose",
                "Barley",
                "Sweet",
                "Green",
                "Sunny",
                "Merry",
            ],
            [
                "wood",
                "brook",
                "hollow",
                "burr",
                "field",
                "worth",
                "bottom",
                "dale",
                "shire",
                "burrow",
                "meadow",
                "hill",
                "vale",
                "croft",
                "glen",
            ]
        ),
        [CreatureType.Gnome] = new NamePool(
            [
                "Cog",
                "Sprocket",
                "Tinker",
                "Spark",
                "Gear",
                "Fizzle",
                "Bramble",
                "Widget",
                "Bolt",
                "Glimmer",
                "Fumble",
                "Nimble",
                "Copper",
                "Bright",
                "Wobble",
            ],
            [
                "ville",
                "gear",
                "wick",
                "spring",
                "junction",
                "burrow",
                "hollow",
                "gate",
                "cog",
                "spire",
                "dell",
                "haven",
                "crest",
                "hatch",
                "works",
            ]
        ),
    };

    public static string GenerateCityName(CreatureType race, ISet<string> existingNames)
    {
        var pool = Pools[race];
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var name =
                pool.Prefixes[Random.Shared.Next(pool.Prefixes.Length)]
                + pool.Suffixes[Random.Shared.Next(pool.Suffixes.Length)];
            if (existingNames.Add(name))
            {
                return name;
            }
        }

        var fallback = $"{pool.Prefixes[0]}{pool.Suffixes[0]}{existingNames.Count}";
        existingNames.Add(fallback);
        return fallback;
    }

    public static string GenerateBuildingName(
        CreatureType race,
        BuildingType buildingType,
        ISet<string> existingNames
    )
    {
        if (race != CreatureType.Human && BuildingNouns.TryGetValue(buildingType, out var nouns))
        {
            var pool = Pools[race];
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var prefix = pool.Prefixes[Random.Shared.Next(pool.Prefixes.Length)];
                var noun = nouns[Random.Shared.Next(nouns.Length)];
                var name = $"The {prefix} {noun}";
                if (existingNames.Add(name))
                {
                    return name;
                }
            }

            var fallback = $"The {pool.Prefixes[0]} {nouns[0]} {existingNames.Count}";
            existingNames.Add(fallback);
            return fallback;
        }

        var staticNames = BuildingGenerator.Names[buildingType];
        var unused = staticNames.Where(n => !existingNames.Contains(n)).ToArray();
        var picked =
            unused.Length > 0
                ? unused[Random.Shared.Next(unused.Length)]
                : staticNames[Random.Shared.Next(staticNames.Length)];
        existingNames.Add(picked);
        return picked;
    }

    public static string GenerateRoadName(CreatureType race, ISet<string> existingNames)
    {
        var pool = Pools[race];
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var prefix = pool.Prefixes[Random.Shared.Next(pool.Prefixes.Length)];
            var noun = RoadNouns[Random.Shared.Next(RoadNouns.Length)];
            var name = $"The {prefix} {noun}";
            if (existingNames.Add(name))
            {
                return name;
            }
        }

        var fallback = $"Road {existingNames.Count + 1}";
        existingNames.Add(fallback);
        return fallback;
    }

    public static string GenerateWildernessName(CreatureType race, ISet<string> excludedNames)
    {
        var pool = Pools[race];
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var prefix = pool.Prefixes[Random.Shared.Next(pool.Prefixes.Length)];
            var noun = WildernessNouns[Random.Shared.Next(WildernessNouns.Length)];
            var name = $"{prefix} {noun}";
            if (!excludedNames.Contains(name))
            {
                return name;
            }
        }

        for (var index = 1; ; index++)
        {
            var fallback = $"The {pool.Prefixes[0]} Wilds {index}";
            if (!excludedNames.Contains(fallback))
            {
                return fallback;
            }
        }
    }

    private record NamePool(string[] Prefixes, string[] Suffixes);
}
