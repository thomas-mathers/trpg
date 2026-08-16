using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Worlds.Algorithms;
using TRPG.Domain.Models;
using static TRPG.Application.Worlds.Generators.ItemModifierHelpers;

namespace TRPG.Application.Worlds.Generators;

internal record CreatureGeneratorInput(
    CreatureType CreatureType,
    CreatureArchetype Archetype,
    Guid WorldId,
    Guid BirthLocationId,
    int MinLevel,
    int MaxLevel,
    string? Name = null,
    Gender? Gender = null,
    int? MinBirthYear = null,
    int? MaxBirthYear = null,
    IReadOnlyDictionary<AllocatableAttributeName, int>? StartingAttributeAllocation = null
);

public record CreatureGeneratorResult(
    Creature Creature,
    IReadOnlyList<Item> Items,
    IReadOnlyCollection<CreatureSkill> Skills
);

public class CreatureGenerator(
    ItemGenerator itemGenerator,
    IOptionsSnapshot<CreatureGeneratorOptions> optionsSnapshot
)
{
    private static readonly NamePool HumanPool = new(
        [
            "Alden",
            "Alric",
            "Ansel",
            "Aric",
            "Baldric",
            "Bennet",
            "Beric",
            "Brand",
            "Brenner",
            "Cedric",
            "Corwin",
            "Darian",
            "Edric",
            "Edwin",
            "Eldric",
            "Emric",
            "Errol",
            "Falk",
            "Gareth",
            "Gavin",
            "Godric",
            "Hadrian",
            "Halric",
            "Harwin",
            "Jareth",
            "Kellan",
            "Leoric",
            "Lucan",
            "Merrick",
            "Osric",
            "Roderic",
            "Roland",
            "Stefan",
            "Theron",
            "Tristan",
            "Ulric",
            "Wulfric",
            "Alaric",
            "Bastian",
            "Calder",
            "Aldous",
            "Barnaby",
            "Cassian",
            "Dorian",
            "Edmund",
            "Garrick",
            "Holt",
            "Percival",
            "Rowan",
            "Warrick",
        ],
        [
            "Adela",
            "Alena",
            "Anya",
            "Brienne",
            "Brynn",
            "Celia",
            "Clarice",
            "Delia",
            "Edith",
            "Elena",
            "Elise",
            "Evelyn",
            "Fiona",
            "Freya",
            "Gwen",
            "Helena",
            "Isolde",
            "Jocelyn",
            "Kaela",
            "Liora",
            "Lyanna",
            "Mara",
            "Maris",
            "Meriel",
            "Mirabel",
            "Nerys",
            "Roslyn",
            "Rowena",
            "Selene",
            "Serena",
            "Sylva",
            "Talia",
            "Thalia",
            "Valena",
            "Vera",
            "Vivienne",
            "Ysolde",
            "Arwen",
            "Catrin",
            "Maeve",
            "Cassandra",
            "Elowen",
            "Ginevra",
            "Iona",
            "Marguerite",
            "Odessa",
            "Perpetua",
            "Wilhelmina",
            "Ysabel",
            "Wren",
        ],
        [
            "Ashford",
            "Ashmere",
            "Blackwood",
            "Briar",
            "Brighton",
            "Coldbrook",
            "Dawnmere",
            "Dunridge",
            "Eastmere",
            "Fairchild",
            "Falconer",
            "Fenwick",
            "Frost",
            "Goldwell",
            "Graycastle",
            "Greymark",
            "Hawthorne",
            "Highmore",
            "Hillcrest",
            "Ironwood",
            "Kingsley",
            "Larkspur",
            "Longford",
            "Marwood",
            "Mournhill",
            "Northmere",
            "Oakheart",
            "Ravencrest",
            "Redbrook",
            "Riverstone",
            "Silverbrook",
            "Stonebridge",
            "Stormholt",
            "Thorne",
            "Valewood",
            "Westbrook",
            "Whitehill",
            "Wintermere",
            "Wolfhart",
            "Woodcroft",
            "Ashvale",
            "Barrowfield",
            "Blackthorn",
            "Briarwood",
            "Cinderfall",
            "Dunmoor",
            "Eastwick",
            "Elmsworth",
            "Fallowmere",
            "Farrow",
            "Foxglove",
            "Graywood",
            "Greenvale",
            "Hallowell",
            "Harrowgate",
            "Ironcastle",
            "Kestrel",
            "Lockhart",
            "Millbrook",
            "Nightingale",
            "Oakhurst",
            "Pemberton",
            "Ravensworth",
            "Rosewood",
            "Shadowmere",
            "Summerfield",
            "Thistlewood",
            "Vane",
            "Wintercross",
            "Wyndham",
        ]
    );

    private static readonly NamePool ElfPool = new(
        [
            "Aerendyl",
            "Aelar",
            "Arannis",
            "Caeleth",
            "Daenor",
            "Elaith",
            "Faelar",
            "Galadrian",
            "Laeroth",
            "Lorindel",
            "Rhovan",
            "Saelihn",
            "Selanar",
            "Silvyr",
            "Thamior",
            "Theren",
            "Vaeril",
            "Ylyndar",
            "Zeren",
            "Erevan",
            "Aramil",
            "Beiro",
            "Dayereth",
            "Enialis",
            "Immeral",
            "Mindartis",
            "Paelias",
            "Peren",
            "Soveliss",
            "Varis",
        ],
        [
            "Althaea",
            "Arwen",
            "Eilistra",
            "Elenwe",
            "Elowen",
            "Ithilwen",
            "Kaelith",
            "Lethariel",
            "Lirael",
            "Maeralya",
            "Melian",
            "Miriel",
            "Naivara",
            "Nimrodel",
            "Nuala",
            "Shalana",
            "Sylvaris",
            "Sylwen",
            "Thalindra",
            "Vaelith",
            "Vanya",
            "Anastrianna",
            "Antinua",
            "Bethrynna",
            "Caelynn",
            "Drusilia",
            "Ielenia",
            "Keyleth",
            "Sariel",
            "Shanairra",
        ],
        [
            "Amakiir",
            "Brightwater",
            "Dawnstrider",
            "Duskbrook",
            "Evenstar",
            "Goldenleaf",
            "Lightbringer",
            "Moonbrook",
            "Moonwhisper",
            "Nightbloom",
            "Silverbranch",
            "Silverleaf",
            "Starfall",
            "Starweaver",
            "Sunshadow",
            "Swiftbrook",
            "Whisperwind",
            "Willowmere",
            "Windrunner",
            "Winterbough",
            "Wyldwood",
            "Mistwalker",
            "Shadowmere",
            "Greenbriar",
            "Ashgrove",
            "Autumnwind",
            "Brightspear",
            "Dawnsinger",
            "Emberleaf",
            "Fallowbrook",
            "Frostwhisper",
            "Glimmerdale",
            "Hollowbrook",
            "Ironflower",
            "Lightweaver",
            "Mistbourne",
            "Moonshadow",
            "Nightwhisper",
            "Oakenshield",
            "Ravenmoor",
            "Silvermist",
            "Stormrider",
            "Sunwhisper",
            "Thornbrook",
            "Wintermoon",
        ]
    );

    private static readonly NamePool DwarfPool = new(
        [
            "Anrik",
            "Balgrim",
            "Bofri",
            "Borin",
            "Bruni",
            "Dain",
            "Dorin",
            "Dunric",
            "Durgan",
            "Eitri",
            "Grimnar",
            "Harbek",
            "Kadrin",
            "Morgran",
            "Nordri",
            "Orsik",
            "Rurik",
            "Sindri",
            "Thorgrim",
            "Thrain",
            "Thror",
            "Travok",
            "Ulfar",
            "Vondal",
            "Baldor",
            "Grundi",
            "Hargin",
            "Korin",
            "Bruenor",
            "Darrak",
            "Delg",
            "Fargrim",
            "Oskar",
            "Tordek",
            "Veit",
        ],
        [
            "Astrid",
            "Balina",
            "Dagnal",
            "Durga",
            "Freygund",
            "Helga",
            "Hilde",
            "Ketra",
            "Sigrun",
            "Torbera",
            "Vala",
            "Vistra",
            "Amber",
            "Artin",
            "Audhild",
            "Bardryn",
            "Diesa",
            "Eldeth",
            "Falkrunn",
            "Finellen",
            "Gunnloda",
            "Gurdis",
            "Helja",
            "Hlin",
            "Kathra",
            "Kristryd",
            "Liftrasa",
            "Torgga",
            "Mardred",
            "Riswynn",
            "Sannl",
            "Dagmar",
            "Gudrun",
            "Ingrid",
            "Ragna",
        ],
        [
            "Anvilfist",
            "Battleaxe",
            "Bronzebeard",
            "Coalbeard",
            "Deepdelver",
            "Emberforge",
            "Firemantle",
            "Forgehammer",
            "Goldfinder",
            "Granitehelm",
            "Grimforge",
            "Hammerfall",
            "Ironbeard",
            "Ironforge",
            "Ironjaw",
            "Rockfist",
            "Stonebeard",
            "Stonebreaker",
            "Stonehammer",
            "Steelheart",
            "Strongpick",
            "Deepstone",
            "Oreseeker",
            "Runehammer",
            "Blackanvil",
            "Boulderfist",
            "Coppervein",
            "Copperforge",
            "Deephammer",
            "Emberbeard",
            "Flintlock",
            "Goldbeard",
            "Graniteforge",
            "Hardstone",
            "Ironclad",
            "Mountainheart",
            "Oreforge",
            "Quarryson",
            "Redbeard",
            "Rockbreaker",
            "Silverpick",
            "Stonefist",
            "Steelforge",
            "Thundershield",
            "Warhammer",
        ]
    );

    private static readonly NamePool OrcPool = new(
        [
            "Ghak",
            "Ghazbul",
            "Gorak",
            "Grukk",
            "Grul",
            "Karg",
            "Krusk",
            "Mogthar",
            "Morgash",
            "Skarn",
            "Thokk",
            "Throggar",
            "Ugluk",
            "Urgan",
            "Uzgash",
            "Uzarg",
            "Varg",
            "Vashak",
            "Vorak",
            "Vorn",
            "Yarg",
            "Zogar",
            "Zulgar",
            "Brug",
            "Grom",
            "Hruk",
            "Krosh",
            "Murg",
            "Torzug",
            "Urgash",
            "Dench",
            "Feng",
            "Gell",
            "Henk",
            "Holg",
        ],
        [
            "Brakka",
            "Drakka",
            "Kagra",
            "Krenna",
            "Nagga",
            "Ruzka",
            "Ulzara",
            "Zagga",
            "Rakka",
            "Shura",
            "Baggi",
            "Emen",
            "Engong",
            "Kansif",
            "Myev",
            "Neega",
            "Ovak",
            "Ownka",
            "Shautha",
            "Sutha",
            "Vola",
            "Volen",
            "Yevelda",
            "Mogra",
            "Skarza",
            "Vorka",
            "Grazna",
            "Uzka",
            "Thura",
            "Zulga",
            "Draka",
            "Geyah",
            "Grazka",
            "Morza",
            "Zulkra",
        ],
        [
            "Ashclaw",
            "Bloodfang",
            "Bonegnaw",
            "Bonecrusher",
            "Doomhowl",
            "Deathgrip",
            "Grimtusk",
            "Gutripper",
            "Ironhide",
            "Ragefist",
            "Rotmaw",
            "Skullcrusher",
            "Skullsplitter",
            "Skinflayer",
            "Spinebreaker",
            "Stormmaw",
            "The Butcher",
            "The Ravager",
            "Warscar",
            "Wolfkiller",
            "Blacktooth",
            "Redblade",
            "Fleshtearer",
            "Gravemaw",
            "Hellscar",
            "Axebiter",
            "Boneshatter",
            "Bloodmaw",
            "Deathfang",
            "Doomfist",
            "Firescar",
            "Gorehowl",
            "Ironmaw",
            "Nightclaw",
            "Painbringer",
            "Ragescar",
            "Rotgrip",
            "Scarhide",
            "Skullbiter",
            "Stormfang",
            "Thornmaw",
            "Warhowl",
            "Wolfscar",
            "Bloodrend",
            "Grimhowl",
        ]
    );

    private static readonly NamePool HalflingPool = new(
        [
            "Alton",
            "Dudo",
            "Fenwick",
            "Finnan",
            "Fosco",
            "Hamish",
            "Hob",
            "Jasper",
            "Merri",
            "Milo",
            "Perrin",
            "Pip",
            "Samwise",
            "Tobias",
            "Wendell",
            "Roscoe",
            "Rollo",
            "Wilbur",
            "Hugo",
            "Ander",
            "Wellby",
            "Cade",
            "Corrin",
            "Errich",
            "Garret",
            "Lindal",
            "Lyle",
            "Reed",
        ],
        [
            "Bella",
            "Bramble",
            "Cora",
            "Daisy",
            "Elly",
            "Ivy",
            "Lavinia",
            "Lily",
            "Marigold",
            "Nora",
            "Poppy",
            "Primrose",
            "Rosalind",
            "Ruby",
            "Seraphina",
            "Willow",
            "Minta",
            "Posy",
            "Tansy",
            "Briony",
            "Esme",
            "Carlin",
            "Dahlia",
            "Euphemia",
            "Lavender",
            "Meadow",
            "Peony",
            "Wisteria",
        ],
        [
            "Applewood",
            "Appleford",
            "Berrybrook",
            "Brambleburr",
            "Brushgather",
            "Cobblestone",
            "Fairmeadow",
            "Goodbarrel",
            "Greenhill",
            "Hayworth",
            "Hilltopple",
            "Meadowlight",
            "Oakbottom",
            "Oakhollow",
            "Puddlefoot",
            "Riverburrow",
            "Softstep",
            "Thistledown",
            "Underbough",
            "Underhill",
            "Warmwater",
            "Whitethistle",
            "Honeyfoot",
            "Leafhopper",
            "Tealeaf",
            "Berryfield",
            "Bramblewood",
            "Brookshire",
            "Clovermead",
            "Cottonwood",
            "Elderberry",
            "Fernwhistle",
            "Foxburrow",
            "Goodfellow",
            "Greenbottle",
            "Hollyhock",
            "Honeysuckle",
            "Meadowbrook",
            "Millhouse",
            "Mosswood",
            "Quickfoot",
            "Sweetwater",
            "Tanglefoot",
            "Thornbury",
            "Windmill",
        ]
    );

    private static readonly NamePool GnomePool = new(
        [
            "Alston",
            "Boddynock",
            "Bumblenoot",
            "Dimble",
            "Dobbin",
            "Fizzwick",
            "Gimble",
            "Jebeddo",
            "Nib",
            "Ordo",
            "Pipkin",
            "Sprocket",
            "Tink",
            "Tobin",
            "Widget",
            "Bramblewick",
            "Cog",
            "Fonkin",
            "Pog",
            "Rumpadump",
            "Tock",
            "Yaffle",
            "Zook",
            "Alvyn",
            "Brocc",
            "Burgell",
            "Erky",
            "Namfoodle",
            "Roondar",
            "Wrenn",
        ],
        [
            "Bimble",
            "Frizzle",
            "Glimmerpop",
            "Mardnab",
            "Nooble",
            "Quibble",
            "Rill",
            "Tana",
            "Twyla",
            "Wizzle",
            "Zilly",
            "Dapple",
            "Ellywick",
            "Nissa",
            "Sivli",
            "Whim",
            "Zanna",
            "Bimpnottin",
            "Breena",
            "Donella",
            "Ella",
            "Nyx",
            "Oda",
            "Caramip",
            "Carlin",
            "Duvamil",
            "Lorilla",
            "Orla",
            "Roywyn",
            "Waywocket",
        ],
        [
            "Bramblegear",
            "Bristlecog",
            "Cogsworth",
            "Copperwing",
            "Fidgetgear",
            "Fizzlewhistle",
            "Fumblebrass",
            "Geargrin",
            "Gearwhistle",
            "Nimblecog",
            "Pennywhistle",
            "Quickgear",
            "Rattlebolt",
            "Sparkspinner",
            "Sprocketstitch",
            "Tinkerbolt",
            "Togglegear",
            "Wobblegear",
            "Wondergear",
            "Whistlewick",
            "Clockspinner",
            "Steamcog",
            "Coppercog",
            "Gadgetgrin",
            "Boltspinner",
            "Brasswhistle",
            "Cogwhistle",
            "Dazzlespark",
            "Fizzbang",
            "Gearspring",
            "Glimmerbolt",
            "Ironcog",
            "Jinglegear",
            "Nimblewick",
            "Puzzlegear",
            "Quicksprocket",
            "Rustynut",
            "Silvercog",
            "Sparkwrench",
            "Springheel",
            "Tinkergear",
            "Twistbolt",
            "Whirligig",
            "Wickerspark",
            "Zigzaggear",
        ]
    );

    private static readonly string[] UndeadEpithets =
    [
        "Grim",
        "Rotting",
        "Withered",
        "Hollow",
        "Pale",
        "Cursed",
        "Forsaken",
        "Deathless",
        "Sunken",
        "Moldering",
        "Ashen",
        "Forgotten",
        "Silent",
        "Grave-born",
    ];

    private static readonly NamePool UndeadPool = new(
        UndeadEpithets,
        UndeadEpithets,
        [
            "Ghoul",
            "Revenant",
            "Banshee",
            "Cadaver",
            "Lich",
            "Specter",
            "Corpse",
            "Deadwalker",
            "Bonewalker",
            "Graveborn",
            "Wight",
            "Shroud",
            "Mourner",
            "Deathknell",
        ]
    );

    private static readonly string[] DemonEpithets =
    [
        "Vile",
        "Damned",
        "Infernal",
        "Wicked",
        "Cruel",
        "Malevolent",
        "Scorched",
        "Blackened",
        "Tormented",
        "Depraved",
        "Hellborn",
        "Accursed",
        "Bloodstained",
        "Corrupted",
    ];

    private static readonly NamePool DemonPool = new(
        DemonEpithets,
        DemonEpithets,
        [
            "Fiend",
            "Devilkin",
            "Imp",
            "Hellspawn",
            "Tormentor",
            "Defiler",
            "Brimstone",
            "Malice",
            "Corruptor",
            "Doomcaller",
            "Netherborn",
            "Soulrender",
            "Pyreclaw",
            "Ashfiend",
        ]
    );

    private static readonly string[] BeastEpithets =
    [
        "Feral",
        "Savage",
        "Ravenous",
        "Wild",
        "Snarling",
        "Prowling",
        "Bloodfanged",
        "Rabid",
        "Untamed",
        "Slavering",
        "Fanged",
        "Clawed",
        "Matted",
        "Bristling",
    ];

    private static readonly NamePool BeastPool = new(
        BeastEpithets,
        BeastEpithets,
        [
            "Fang",
            "Claw",
            "Prowler",
            "Stalker",
            "Howler",
            "Ripper",
            "Maw",
            "Talon",
            "Predator",
            "Hunter",
            "Snarler",
            "Gnasher",
            "Rager",
            "Skulker",
        ]
    );

    private static readonly string[] ConstructEpithets =
    [
        "Iron",
        "Stone",
        "Rusted",
        "Forged",
        "Brazen",
        "Hollow",
        "Grinding",
        "Clockwork",
        "Runic",
        "Gearbound",
        "Ironclad",
        "Chiseled",
        "Riveted",
        "Leaden",
    ];

    private static readonly NamePool ConstructPool = new(
        ConstructEpithets,
        ConstructEpithets,
        [
            "Golem",
            "Automaton",
            "Sentinel",
            "Colossus",
            "Warden",
            "Juggernaut",
            "Guardian",
            "Mechanism",
            "Effigy",
            "Bulwark",
            "Statuary",
            "Cog",
            "Anvilborn",
            "Stoneheart",
        ]
    );

    private static readonly string[] ElementalEpithets =
    [
        "Burning",
        "Freezing",
        "Roaring",
        "Churning",
        "Molten",
        "Crackling",
        "Howling",
        "Surging",
        "Volcanic",
        "Glacial",
        "Thundering",
        "Swirling",
        "Blazing",
        "Rippling",
    ];

    private static readonly NamePool ElementalPool = new(
        ElementalEpithets,
        ElementalEpithets,
        [
            "Cinder",
            "Torrent",
            "Gale",
            "Boulder",
            "Ember",
            "Frost",
            "Cyclone",
            "Magma",
            "Squall",
            "Geyser",
            "Avalanche",
            "Firestorm",
            "Undertow",
            "Whirlwind",
        ]
    );

    private static readonly string[] GoblinEpithets =
    [
        "Sneaky",
        "Grubby",
        "Snivel",
        "Filthy",
        "Cackling",
        "Scrawny",
        "Sniveling",
        "Ratty",
        "Grimy",
        "Impish",
        "Sly",
        "Yellow-toothed",
        "Scabby",
        "Twitchy",
    ];

    private static readonly NamePool GoblinPool = new(
        GoblinEpithets,
        GoblinEpithets,
        [
            "Snagtooth",
            "Grubfinger",
            "Ratbite",
            "Muckwallow",
            "Snivelrat",
            "Gutgnash",
            "Filchpocket",
            "Backstab",
            "Gnashtooth",
            "Sneakwart",
            "Boneyip",
            "Rustynail",
            "Scrapfang",
            "Puswhistle",
        ]
    );

    private static readonly string[] WraithEpithets =
    [
        "Whispering",
        "Ghostly",
        "Ethereal",
        "Mournful",
        "Weeping",
        "Shrouded",
        "Fading",
        "Silent",
        "Spectral",
        "Haunting",
        "Veiled",
        "Drifting",
        "Wandering",
        "Sorrowful",
    ];

    private static readonly NamePool WraithPool = new(
        WraithEpithets,
        WraithEpithets,
        [
            "Wisp",
            "Echo",
            "Mist",
            "Sorrow",
            "Lament",
            "Gloom",
            "Veil",
            "Whisper",
            "Chill",
            "Phantasm",
            "Remnant",
            "Nightmist",
            "Farshade",
            "Hollowmoan",
        ]
    );

    private static readonly string[] GiantEpithets =
    [
        "Towering",
        "Mighty",
        "Colossal",
        "Thunderous",
        "Bone-crushing",
        "Mountainous",
        "Lumbering",
        "Hulking",
        "Earthshaking",
        "Massive",
        "Brutish",
        "Titanic",
        "Craggy",
        "Broadshouldered",
    ];

    private static readonly NamePool GiantPool = new(
        GiantEpithets,
        GiantEpithets,
        [
            "Crusher",
            "Stonefist",
            "Skullbreaker",
            "Boulderthrow",
            "Cloudreach",
            "Earthshaker",
            "Highstrider",
            "Mountainborn",
            "Landbreaker",
            "Ridgeback",
            "Thunderfoot",
            "Hillstomper",
            "Rockjaw",
            "Timberfell",
        ]
    );

    private static readonly string[] DragonEpithets =
    [
        "Ancient",
        "Wyrmborn",
        "Scaled",
        "Fireforged",
        "Skytorn",
        "Eternal",
        "Dread",
        "Stormwreathed",
        "Goldeneyed",
        "Coiled",
        "Venomous",
        "Sovereign",
        "Sunscaled",
        "Nightscaled",
    ];

    private static readonly NamePool DragonPool = new(
        DragonEpithets,
        DragonEpithets,
        [
            "Wyrm",
            "Drake",
            "Scaletail",
            "Flameheart",
            "Emberwing",
            "Frostbane",
            "Stormwing",
            "Bloodscale",
            "Nightwing",
            "Doomwing",
            "Skyrender",
            "Ashwing",
            "Starclaw",
            "Ironscale",
        ]
    );

    private static readonly Dictionary<CreatureType, NamePool[]> Pools = new()
    {
        [CreatureType.Human] = [HumanPool],
        [CreatureType.Elf] = [ElfPool],
        [CreatureType.Dwarf] = [DwarfPool],
        [CreatureType.Orc] = [OrcPool],
        [CreatureType.Halfling] = [HalflingPool],
        [CreatureType.Gnome] = [GnomePool],
        [CreatureType.Undead] = [UndeadPool],
        [CreatureType.Demon] = [DemonPool],
        [CreatureType.Beast] = [BeastPool],
        [CreatureType.Construct] = [ConstructPool],
        [CreatureType.Elemental] = [ElementalPool],
        [CreatureType.Goblin] = [GoblinPool],
        [CreatureType.Wraith] = [WraithPool],
        [CreatureType.Giant] = [GiantPool],
        [CreatureType.Dragon] = [DragonPool],
    };

    internal CreatureGeneratorResult Generate(CreatureGeneratorInput generatorInput)
    {
        var archetype = generatorInput.Archetype;
        var level = Random.Shared.Next(generatorInput.MinLevel, generatorInput.MaxLevel + 1);

        var gender =
            generatorInput.Gender ?? Random.Shared.GetItems(Enum.GetValues<Gender>(), 1).First();

        var isPlayer = generatorInput.StartingAttributeAllocation is not null;
        var attributes = generatorInput.StartingAttributeAllocation is { } allocation
            ? GetPlayerAttributes(level, allocation)
            : GetAttributes(level, archetype);

        var creatureType = archetype.CreatureType ?? generatorInput.CreatureType;

        var creature = new Creature
        {
            WorldId = generatorInput.WorldId,
            Name = generatorInput.Name ?? GetName(creatureType, gender),
            CreatureType = creatureType,
            Gender = gender,
            Profession = archetype.Profession,
            Biography = archetype.Biography ?? "",
            BirthLocationId = generatorInput.BirthLocationId,
            BirthYear = Random.Shared.Next(
                generatorInput.MinBirthYear ?? 900,
                generatorInput.MaxBirthYear ?? 975
            ),
            BaseAttributes = attributes,
            LastRegenPlaytime = TimeSpan.Zero,
            Level = level,
            State = CreatureState.Idle,
            NaturalWeaponMinDamage = Roll(
                level,
                archetype.NaturalWeaponDamage.MinDamageLow,
                archetype.NaturalWeaponDamage.MinDamageHigh
            ),
            NaturalWeaponMaxDamage = Roll(
                level,
                archetype.NaturalWeaponDamage.MaxDamageLow,
                archetype.NaturalWeaponDamage.MaxDamageHigh
            ),
        };

        var startingGold = GetGold(level, archetype);
        var items = GenerateStartingInventory(creature, archetype).ToList();
        if (startingGold > 0)
        {
            items.Add(
                new Gold
                {
                    WorldId = creature.WorldId,
                    Name = "Gold",
                    Quantity = startingGold,
                    Ownership = new ItemOwnership
                    {
                        OwnerId = creature.Id,
                        OwnerType = OwnerType.Creature,
                    },
                }
            );
        }
        var equippedItems = items.Where(item => item.Ownership.EquippedSlot != null).ToArray();

        StatFormulas.Recalculate(creature, equippedItems);

        creature.CurrentHp = creature.MaximumHp;
        creature.CurrentAp = creature.MaximumAp;
        creature.CurrentMp = creature.MaximumMp;

        var skills = CreatureSkillsGenerator.Generate(
            creature,
            archetype.SkillAffinities,
            isPlayer
        );

        return new CreatureGeneratorResult(creature, items, skills);
    }

    public CreatureGeneratorResult AddStartingPotions(CreatureGeneratorResult result)
    {
        var creature = result.Creature;

        var potions = ConsumableGenerator
            .PotionNamesByResource.Keys.Select(resource =>
                itemGenerator.GenerateConsumable(resource, creature.Level, creature.WorldId)
            )
            .ToArray();

        foreach (var potion in potions)
        {
            potion.Quantity = 1;
            potion.Ownership.OwnerId = creature.Id;
            potion.Ownership.OwnerType = OwnerType.Creature;
        }

        return result with
        {
            Items = [.. result.Items, .. potions],
        };
    }

    private static int GetGold(int level, CreatureArchetype archetype)
    {
        var baseGold = level * 50;
        var spread = Random.Shared.Next((int)(baseGold * 0.8f), (int)(baseGold * 1.2f));
        return (int)(spread * archetype.StatAffinities.GoldMultiplier);
    }

    private Attributes GetAttributes(int level, CreatureArchetype archetype)
    {
        var a = archetype.StatAffinities;
        int[] pool = [a.Strength, a.Dexterity, a.Endurance, a.Stamina, a.Mana, a.Intelligence];
        var baseline = optionsSnapshot.Value.BaseAttributes;
        int[] stats = [1, 1, 1, 1, 1, 1];

        var allocatableBaselineTotal = baseline.Total() - baseline.Defense;
        var draws =
            allocatableBaselineTotal - stats.Length + level * optionsSnapshot.Value.PointsPerLevel;
        for (var i = 0; i < draws; i++)
        {
            stats[WeightedSampler.SampleIndex(pool)]++;
        }

        var baseAttributes = new Attributes
        {
            Strength = stats[0],
            Defense = baseline.Defense,
            Dexterity = stats[1],
            Endurance = stats[2],
            Stamina = stats[3],
            Mana = stats[4],
            Intelligence = stats[5],
        };

        return baseAttributes with
        {
            MaximumHp = StatFormulas.CalculateMaximumHp(baseAttributes, optionsSnapshot.Value),
            MaximumAp = StatFormulas.CalculateMaximumAp(baseAttributes, optionsSnapshot.Value),
            MaximumMp = StatFormulas.CalculateMaximumMp(baseAttributes, optionsSnapshot.Value),
        };
    }

    private static int GetBaselineValue(
        StartingAttributes baseline,
        AllocatableAttributeName attribute
    ) =>
        attribute switch
        {
            AllocatableAttributeName.Strength => baseline.Strength,
            AllocatableAttributeName.Dexterity => baseline.Dexterity,
            AllocatableAttributeName.Endurance => baseline.Endurance,
            AllocatableAttributeName.Stamina => baseline.Stamina,
            AllocatableAttributeName.Mana => baseline.Mana,
            AllocatableAttributeName.Intelligence => baseline.Intelligence,
            _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null),
        };

    private Attributes GetPlayerAttributes(
        int level,
        IReadOnlyDictionary<AllocatableAttributeName, int> allocation
    )
    {
        var requestedTotal = allocation.Values.Sum();
        var availablePoints = level * optionsSnapshot.Value.PointsPerLevel;
        if (requestedTotal > availablePoints)
        {
            throw new InvalidOperationException(
                $"Requested {requestedTotal} attribute points but only {availablePoints} are available."
            );
        }

        var baseline = optionsSnapshot.Value.BaseAttributes;

        if (
            Enum.GetValues<AllocatableAttributeName>()
                .Any(attribute =>
                    GetBaselineValue(baseline, attribute) + allocation.GetValueOrDefault(attribute)
                    < 1
                )
        )
        {
            throw new InvalidOperationException("Attributes cannot go below 1.");
        }

        var baseAttributes = new Attributes
        {
            Strength =
                baseline.Strength + allocation.GetValueOrDefault(AllocatableAttributeName.Strength),
            Defense = baseline.Defense,
            Dexterity =
                baseline.Dexterity
                + allocation.GetValueOrDefault(AllocatableAttributeName.Dexterity),
            Endurance =
                baseline.Endurance
                + allocation.GetValueOrDefault(AllocatableAttributeName.Endurance),
            Stamina =
                baseline.Stamina + allocation.GetValueOrDefault(AllocatableAttributeName.Stamina),
            Mana = baseline.Mana + allocation.GetValueOrDefault(AllocatableAttributeName.Mana),
            Intelligence =
                baseline.Intelligence
                + allocation.GetValueOrDefault(AllocatableAttributeName.Intelligence),
        };

        return baseAttributes with
        {
            MaximumHp = StatFormulas.CalculateMaximumHp(baseAttributes, optionsSnapshot.Value),
            MaximumAp = StatFormulas.CalculateMaximumAp(baseAttributes, optionsSnapshot.Value),
            MaximumMp = StatFormulas.CalculateMaximumMp(baseAttributes, optionsSnapshot.Value),
        };
    }

    private IReadOnlyList<Item> GenerateStartingInventory(
        Creature creature,
        CreatureArchetype archetype
    )
    {
        var startingItems = GetStartingItems(creature, archetype);
        var items = new List<Item>();
        var occupiedSlots = new HashSet<EquipmentSlot>();

        foreach (var (item, quantity, slotOverride) in startingItems)
        {
            item.Quantity = quantity;
            item.Ownership.OwnerId = creature.Id;
            item.Ownership.OwnerType = OwnerType.Creature;

            var resolvedSlot = slotOverride ?? GetDefaultEquipmentSlot(item);
            if (resolvedSlot != null && occupiedSlots.Add(resolvedSlot.Value))
            {
                item.Ownership.EquippedSlot = resolvedSlot;
                if (item is Weapon { IsTwoHanded: true })
                {
                    occupiedSlots.Add(EquipmentSlot.LeftHand);
                }
            }

            items.Add(item);
        }

        return items;
    }

    private static EquipmentSlot? GetDefaultEquipmentSlot(Item item) =>
        item switch
        {
            Weapon => EquipmentSlot.RightHand,
            Shield => EquipmentSlot.LeftHand,
            Ammunition => EquipmentSlot.LeftHand,
            Armor armor => armor.Type switch
            {
                ArmorType.Helm => EquipmentSlot.Helm,
                ArmorType.Chest => EquipmentSlot.Chest,
                ArmorType.Boots => EquipmentSlot.Boots,
                ArmorType.Gloves => EquipmentSlot.Gloves,
                _ => null,
            },
            Accessory accessory => accessory.Type switch
            {
                AccessoryType.Necklace => EquipmentSlot.Necklace,
                AccessoryType.Belt => EquipmentSlot.Belt,
                AccessoryType.Ring => EquipmentSlot.LeftRing,
                _ => null,
            },
            _ => null,
        };

    private StartingItem[] GetStartingItems(Creature creature, CreatureArchetype archetype)
    {
        var level = creature.Level;
        var worldId = creature.WorldId;

        var items = archetype.StartingGear.Select(spec => GetGearItem(spec, level, worldId));

        if (archetype.ArmorClass is { } armorClass)
        {
            items = items.Concat(GetArmorItems(armorClass, level, worldId));
        }

        if (archetype.HasAccessories)
        {
            items = items.Concat(GetAccessoryItems(level, worldId));
        }

        return items.ToArray();
    }

    private StartingItem GetGearItem(StartingGearSpec spec, int level, Guid worldId) =>
        spec switch
        {
            WeaponSpec weapon => new StartingItem(
                itemGenerator.GenerateWeapon(weapon.WeaponType, level, worldId),
                1,
                weapon.SlotOverride
            ),
            ShieldSpec => new StartingItem(itemGenerator.GenerateShield(level, worldId), 1),
            AmmoSpec ammo => new StartingItem(
                itemGenerator.GenerateAmmo(ammo.AmmoType, worldId),
                ammo.Quantity
            ),
            ConsumableSpec consumable => new StartingItem(
                itemGenerator.GenerateConsumable(level, worldId),
                consumable.Quantity
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec, null),
        };

    private StartingItem[] GetArmorItems(ArmorClass armorClass, int level, Guid worldId)
    {
        return
        [
            new StartingItem(
                itemGenerator.GenerateArmor(ArmorType.Helm, armorClass, level, worldId),
                1
            ),
            new StartingItem(
                itemGenerator.GenerateArmor(ArmorType.Chest, armorClass, level, worldId),
                1
            ),
            new StartingItem(
                itemGenerator.GenerateArmor(ArmorType.Gloves, armorClass, level, worldId),
                1
            ),
            new StartingItem(
                itemGenerator.GenerateArmor(ArmorType.Boots, armorClass, level, worldId),
                1
            ),
        ];
    }

    private StartingItem[] GetAccessoryItems(int level, Guid worldId)
    {
        return
        [
            new StartingItem(
                itemGenerator.GenerateAccessory(AccessoryType.Necklace, level, worldId),
                1
            ),
            new StartingItem(
                itemGenerator.GenerateAccessory(AccessoryType.Belt, level, worldId),
                1
            ),
            new StartingItem(
                itemGenerator.GenerateAccessory(AccessoryType.Ring, level, worldId),
                1
            ),
            new StartingItem(
                itemGenerator.GenerateAccessory(AccessoryType.Ring, level, worldId),
                1,
                EquipmentSlot.RightRing
            ),
        ];
    }

    private static readonly Dictionary<CreatureType, double> MiddleNameChanceByRace = new()
    {
        [CreatureType.Human] = 0.3,
    };

    private static readonly Dictionary<CreatureType, double> ExtraSurnameChanceByRace = new()
    {
        [CreatureType.Dwarf] = 0.5,
        [CreatureType.Gnome] = 0.4,
        [CreatureType.Elf] = 0.3,
        [CreatureType.Halfling] = 0.3,
        [CreatureType.Orc] = 0.2,
    };

    public static string GetName(CreatureType creatureType, Gender gender)
    {
        var firstName = GetFirstName(creatureType, gender);
        var lastName = GetLastName(creatureType);
        return ComposeFullName(creatureType, gender, firstName, lastName);
    }

    public static string ComposeFullName(
        CreatureType creatureType,
        Gender gender,
        string firstName,
        string lastName
    )
    {
        if (
            MiddleNameChanceByRace.TryGetValue(creatureType, out var middleNameChance)
            && Random.Shared.NextDouble() < middleNameChance
        )
        {
            var middleName = GetMiddleName(creatureType, gender, firstName);
            if (middleName != null)
            {
                return $"{firstName} {middleName} {lastName}";
            }
        }

        if (
            ExtraSurnameChanceByRace.TryGetValue(creatureType, out var extraSurnameChance)
            && Random.Shared.NextDouble() < extraSurnameChance
        )
        {
            var extraSurname = GetExtraSurname(creatureType, lastName);
            if (extraSurname != null)
            {
                return $"{firstName} {extraSurname} {lastName}";
            }
        }

        return $"{firstName} {lastName}";
    }

    public static string GetFirstName(CreatureType creatureType, Gender gender)
    {
        var pool = GetPool(creatureType);
        var firstNames = gender == Gender.Male ? pool.MaleFirstNames : pool.FemaleFirstNames;
        return firstNames[Random.Shared.Next(firstNames.Length)];
    }

    private const double DominantRaceWeight = 0.7;

    public static CreatureType PickCreatureType(CreatureType dominantRace)
    {
        if (Random.Shared.NextDouble() < DominantRaceWeight)
        {
            return dominantRace;
        }

        var others = CreatureTypes.Humanoid.Where(r => r != dominantRace).ToArray();
        return others[Random.Shared.Next(others.Length)];
    }

    public static string GetLastName(CreatureType creatureType)
    {
        var pool = GetPool(creatureType);
        return pool.LastNames[Random.Shared.Next(pool.LastNames.Length)];
    }

    private static string? GetMiddleName(CreatureType creatureType, Gender gender, string firstName)
    {
        var pool = GetPool(creatureType);
        var firstNames = gender == Gender.Male ? pool.MaleFirstNames : pool.FemaleFirstNames;
        var candidates = firstNames.Where(n => n != firstName).ToArray();
        return candidates.Length > 0 ? candidates[Random.Shared.Next(candidates.Length)] : null;
    }

    private static string? GetExtraSurname(CreatureType creatureType, string lastName)
    {
        var pool = GetPool(creatureType);
        var candidates = pool.LastNames.Where(n => n != lastName).ToArray();
        return candidates.Length > 0 ? candidates[Random.Shared.Next(candidates.Length)] : null;
    }

    private static NamePool GetPool(CreatureType creatureType)
    {
        var pools = Pools.GetValueOrDefault(creatureType, [BeastPool]);
        return pools[Random.Shared.Next(pools.Length)];
    }

    private record NamePool(string[] MaleFirstNames, string[] FemaleFirstNames, string[] LastNames);

    private record StartingItem(Item Item, int Quantity, EquipmentSlot? SlotOverride = null);
}
