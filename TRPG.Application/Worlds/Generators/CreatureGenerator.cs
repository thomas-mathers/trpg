using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Common;
using TRPG.Application.Creatures;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public record CreatureGeneratorInput(
    CreatureType CreatureType,
    Profession Profession,
    Guid WorldId,
    Guid BirthStateId,
    Guid StateId,
    int Level = 0,
    string? Name = null,
    Gender? Gender = null,
    int? MinBirthYear = null,
    int? MaxBirthYear = null
);

public record CreatureGeneratorResult(
    Creature Creature,
    IReadOnlyList<Item> Items,
    IReadOnlyList<InventoryItem> InventoryItems,
    IReadOnlyCollection<CreatureSkill> Skills,
    IReadOnlyCollection<CreatureAbility> Abilities
);

public class CreatureGenerator(
    ItemGenerator itemGenerator,
    AbilityDefinitions abilityDefinitions,
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

    private static readonly string[] MonsterEpithets =
    [
        "Grim",
        "Foul",
        "Ancient",
        "Feral",
        "Ravenous",
        "Withered",
        "Nameless",
        "Restless",
    ];

    private static readonly NamePool MonsterPool = new(
        MonsterEpithets,
        MonsterEpithets,
        ["Wraith", "Husk", "Fiend", "Beast", "Horror", "Shade", "Abomination", "Stalker"]
    );

    private static readonly Dictionary<CreatureType, NamePool[]> Pools = new()
    {
        [CreatureType.Human] = [HumanPool],
        [CreatureType.Elf] = [ElfPool],
        [CreatureType.Dwarf] = [DwarfPool],
        [CreatureType.Orc] = [OrcPool],
        [CreatureType.Halfling] = [HalflingPool],
        [CreatureType.Gnome] = [GnomePool],
        [CreatureType.Undead] = [MonsterPool],
        [CreatureType.Demon] = [MonsterPool],
        [CreatureType.Beast] = [MonsterPool],
        [CreatureType.Construct] = [MonsterPool],
        [CreatureType.Elemental] = [MonsterPool],
    };

    private static readonly Dictionary<Profession, ArmorClass> ProfessionArmorClasses = new()
    {
        [Profession.Knight] = ArmorClass.Plate,
        [Profession.Rogue] = ArmorClass.Leather,
        [Profession.Ranger] = ArmorClass.Leather,
        [Profession.Mage] = ArmorClass.Cloth,
        [Profession.Cleric] = ArmorClass.Plate,
        [Profession.Mercenary] = ArmorClass.Mail,
        [Profession.Alchemist] = ArmorClass.Cloth,
        [Profession.Blacksmith] = ArmorClass.Plate,
        [Profession.Scholar] = ArmorClass.Cloth,
        [Profession.Merchant] = ArmorClass.Leather,
        [Profession.Politician] = ArmorClass.Cloth,
        [Profession.StableMaster] = ArmorClass.Leather,
        [Profession.Bartender] = ArmorClass.Cloth,
        [Profession.Guard] = ArmorClass.Mail,
        [Profession.Baker] = ArmorClass.Cloth,
        [Profession.Innkeeper] = ArmorClass.Cloth,
        [Profession.Tailor] = ArmorClass.Cloth,
        [Profession.Carpenter] = ArmorClass.Leather,
        [Profession.Jeweler] = ArmorClass.Cloth,
        [Profession.Homemaker] = ArmorClass.Cloth,
        [Profession.Unemployed] = ArmorClass.Cloth,
    };

    private static readonly Dictionary<Profession, Skill[]> ProfessionSkills = new()
    {
        [Profession.Knight] = [Skill.Swordsmanship, Skill.Warfare],
        [Profession.Rogue] = [Skill.Stealth],
        [Profession.Ranger] = [Skill.Archery],
        [Profession.Mage] = [Skill.Spellcasting],
        [Profession.Cleric] = [Skill.Devotion, Skill.Warfare],
        [Profession.Mercenary] = [Skill.Swordsmanship, Skill.Warfare],
        [Profession.Alchemist] = [Skill.Spellcasting],
        [Profession.Blacksmith] = [Skill.Warfare],
        [Profession.Scholar] = [Skill.Spellcasting],
        [Profession.Merchant] = [],
        [Profession.Politician] = [],
        [Profession.StableMaster] = [],
        [Profession.Bartender] = [],
        [Profession.Guard] = [Skill.Swordsmanship, Skill.Warfare],
        [Profession.Baker] = [],
        [Profession.Innkeeper] = [],
        [Profession.Tailor] = [],
        [Profession.Carpenter] = [],
        [Profession.Jeweler] = [],
        [Profession.Homemaker] = [],
        [Profession.Unemployed] = [],
    };

    private static readonly Dictionary<Profession, StatAffinities> Affinities = new()
    {
        [Profession.Knight] = new StatAffinities(3, 3, 0, 2, 2, 0, 0, 0.8f),
        [Profession.Rogue] = new StatAffinities(1, 0, 4, 1, 2, 0, 2, 1.2f),
        [Profession.Ranger] = new StatAffinities(1, 0, 3, 2, 2, 0, 2, 0.9f),
        [Profession.Mage] = new StatAffinities(0, 0, 0, 1, 0, 4, 5, 1.5f),
        [Profession.Cleric] = new StatAffinities(0, 2, 0, 1, 1, 3, 3, 1.0f),
        [Profession.Mercenary] = new StatAffinities(3, 2, 1, 1, 3, 0, 0, 1.1f),
        [Profession.Alchemist] = new StatAffinities(0, 0, 2, 1, 0, 3, 4, 2.0f),
        [Profession.Blacksmith] = new StatAffinities(4, 1, 1, 3, 1, 0, 0, 1.5f),
        [Profession.Scholar] = new StatAffinities(0, 0, 1, 1, 0, 1, 7, 2.0f),
        [Profession.Merchant] = new StatAffinities(0, 0, 3, 1, 1, 0, 5, 3.0f),
        [Profession.Politician] = new StatAffinities(0, 0, 1, 0, 0, 1, 8, 4.0f),
        [Profession.StableMaster] = new StatAffinities(1, 0, 3, 3, 2, 0, 1, 1.0f),
        [Profession.Bartender] = new StatAffinities(0, 0, 3, 1, 2, 0, 4, 1.2f),
        [Profession.Guard] = new StatAffinities(2, 3, 1, 3, 1, 0, 0, 0.7f),
        [Profession.Baker] = new StatAffinities(1, 0, 2, 2, 2, 0, 2, 1.3f),
        [Profession.Innkeeper] = new StatAffinities(0, 0, 2, 1, 2, 0, 3, 1.4f),
        [Profession.Tailor] = new StatAffinities(0, 0, 3, 1, 1, 0, 3, 1.5f),
        [Profession.Carpenter] = new StatAffinities(2, 0, 2, 2, 2, 0, 1, 1.2f),
        [Profession.Jeweler] = new StatAffinities(0, 0, 3, 0, 1, 0, 3, 2.5f),
        [Profession.Homemaker] = new StatAffinities(0, 0, 1, 2, 2, 0, 1, 0.5f),
        [Profession.Unemployed] = new StatAffinities(0, 0, 1, 1, 1, 0, 1, 0.3f),
    };

    private static readonly HashSet<Profession> CombatProfessions =
    [
        Profession.Knight,
        Profession.Rogue,
        Profession.Ranger,
        Profession.Mage,
        Profession.Cleric,
        Profession.Mercenary,
        Profession.Guard,
    ];

    private sealed record LevelRange(int Minimum, int Maximum);

    private static readonly LevelRange CombatLevelRange = new(5, 100);
    private static readonly LevelRange CivilianLevelRange = new(1, 20);

    public CreatureGeneratorResult Generate(CreatureGeneratorInput generatorInput)
    {
        var levelRange = CombatProfessions.Contains(generatorInput.Profession)
            ? CombatLevelRange
            : CivilianLevelRange;
        var maximumLevel = Math.Min(levelRange.Maximum, optionsSnapshot.Value.MaxLevel);
        var level =
            generatorInput.Level > 0
                ? generatorInput.Level
                : Random.Shared.Next(levelRange.Minimum, maximumLevel + 1);
        var gender =
            generatorInput.Gender ?? (Random.Shared.Next(2) == 0 ? Gender.Male : Gender.Female);

        var creature = new Creature
        {
            WorldId = generatorInput.WorldId,
            Name = generatorInput.Name ?? GetName(generatorInput.CreatureType, gender),
            CreatureType = generatorInput.CreatureType,
            Gender = gender,
            Profession = generatorInput.Profession,
            BirthStateId = generatorInput.BirthStateId,
            BirthYear = Random.Shared.Next(
                generatorInput.MinBirthYear ?? 900,
                generatorInput.MaxBirthYear ?? 975
            ),
            Gold = GetGold(level, generatorInput.Profession),
            StateId = generatorInput.StateId,
            Attributes = GetAttributes(level, generatorInput.Profession),
            Level = level,
        };

        var (items, inventoryItems) = GenerateStartingInventory(creature);
        var skills = GetSkills(creature);
        var abilities = GetAbilities(creature, skills);

        return new CreatureGeneratorResult(creature, items, inventoryItems, skills, abilities);
    }

    private IReadOnlyCollection<CreatureSkill> GetSkills(Creature creature)
    {
        var skillLevel = Math.Min(creature.Level, optionsSnapshot.Value.MaxSkillLevel);
        return ProfessionSkills[creature.Profession!.Value]
            .Select(skill => new CreatureSkill
            {
                CreatureId = creature.Id,
                Skill = skill,
                Level = skillLevel,
                Experience = XpForSkillLevel(skillLevel),
                WorldId = creature.WorldId,
            })
            .ToArray();
    }

    private static int XpForSkillLevel(int level) => 25 * level * (level + 3);

    private IReadOnlyCollection<CreatureAbility> GetAbilities(
        Creature creature,
        IReadOnlyCollection<CreatureSkill> skills
    )
    {
        return skills
            .SelectMany(cs =>
                abilityDefinitions
                    .Abilities.Where(a => a.Skill == cs.Skill && a.RequiredSkillLevel <= cs.Level)
                    .Select(a => new CreatureAbility
                    {
                        CreatureId = creature.Id,
                        AbilityName = a.Name,
                        WorldId = creature.WorldId,
                    })
            )
            .ToArray();
    }

    private static int GetGold(int level, Profession profession)
    {
        var baseGold = level * 50;
        var spread = Random.Shared.Next((int)(baseGold * 0.8f), (int)(baseGold * 1.2f));
        return (int)(spread * Affinities[profession].GoldMultiplier);
    }

    private Attributes GetAttributes(int level, Profession profession)
    {
        var a = Affinities[profession];
        int[] pool =
        [
            a.Strength,
            a.Defense,
            a.Dexterity,
            a.Endurance,
            a.Stamina,
            a.Mana,
            a.Intelligence,
        ];
        var total = pool.Sum();
        var stats = new int[7];
        Array.Fill(stats, 1);

        for (var i = 0; i < level * optionsSnapshot.Value.PointsPerLevel; i++)
        {
            var roll = Random.Shared.Next(total);
            var cumulative = 0;
            for (var j = 0; j < pool.Length; j++)
            {
                cumulative += pool[j];
                if (roll < cumulative)
                {
                    stats[j]++;
                    break;
                }
            }
        }

        var baseAttributes = new Attributes
        {
            Strength = stats[0],
            Defense = stats[1],
            Dexterity = stats[2],
            Endurance = stats[3],
            Stamina = stats[4],
            Mana = stats[5],
            Intelligence = stats[6],
        };

        return baseAttributes with
        {
            MaximumHp = StatFormulas.CalculateMaximumHp(baseAttributes),
            MaximumAp = StatFormulas.CalculateMaximumAp(baseAttributes),
            MaximumMp = StatFormulas.CalculateMaximumMp(baseAttributes),
        };
    }

    private StartingInventoryResult GenerateStartingInventory(Creature creature)
    {
        var startingItems = GetStartingItems(creature);
        var items = new List<Item>();
        var inventoryItems = new List<InventoryItem>();
        var index = 0;

        foreach (var (item, quantity, slotOverride) in startingItems)
        {
            items.Add(item);
            inventoryItems.Add(
                new InventoryItem
                {
                    CreatureId = creature.Id,
                    ItemId = item.Id,
                    Quantity = quantity,
                    Index = index++,
                    EquippedSlot = slotOverride ?? item.DefaultSlot,
                    WorldId = creature.WorldId,
                }
            );
        }

        return new StartingInventoryResult(items.ToArray(), inventoryItems.ToArray());
    }

    private StartingItem[] GetStartingItems(Creature creature)
    {
        var level = creature.Level;
        var worldId = creature.WorldId;
        var armorClass = ProfessionArmorClasses.GetValueOrDefault(
            creature.Profession!.Value,
            ArmorClass.Leather
        );
        var armor = GetArmorItems(armorClass, level, worldId);
        var accessories = GetAccessoryItems(level, worldId);

        return creature.Profession switch
        {
            Profession.Knight =>
            [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Sword, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateShield(level, worldId), 1),
                .. armor,
                .. accessories,
            ],
            Profession.Rogue =>
            [
                new StartingItem(
                    itemGenerator.GenerateWeapon(WeaponType.Dagger, level, worldId),
                    1
                ),
                new StartingItem(
                    itemGenerator.GenerateWeapon(WeaponType.Dagger, level, worldId),
                    1,
                    EquipmentSlot.LeftHand
                ),
                .. armor,
                .. accessories,
            ],
            Profession.Ranger =>
            [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Bow, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateAmmo(AmmoType.Arrow, worldId), 20),
                .. armor,
                .. accessories,
            ],
            Profession.Mage =>
            [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Staff, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateConsumable(level, worldId), 3),
                .. armor,
                .. accessories,
            ],
            Profession.Cleric =>
            [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Mace, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateShield(level, worldId), 1),
                .. armor,
                .. accessories,
            ],
            Profession.Mercenary =>
            [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Sword, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateShield(level, worldId), 1),
                .. armor,
                .. accessories,
            ],
            Profession.Alchemist =>
            [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Wand, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateConsumable(level, worldId), 5),
                .. armor,
                .. accessories,
            ],
            Profession.Blacksmith =>
            [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Axe, level, worldId), 1),
                .. armor,
                .. accessories,
            ],
            Profession.Scholar =>
            [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Staff, level, worldId), 1),
                .. armor,
                .. accessories,
            ],
            _ =>
            [
                new StartingItem(
                    itemGenerator.GenerateWeapon(WeaponType.Dagger, level, worldId),
                    1
                ),
                .. armor,
                .. accessories,
            ],
        };
    }

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
        var pools = Pools.GetValueOrDefault(creatureType, [MonsterPool]);
        return pools[Random.Shared.Next(pools.Length)];
    }

    private record NamePool(string[] MaleFirstNames, string[] FemaleFirstNames, string[] LastNames);

    private record StatAffinities(
        int Strength,
        int Defense,
        int Dexterity,
        int Endurance,
        int Stamina,
        int Mana,
        int Intelligence,
        float GoldMultiplier
    );

    private record StartingInventoryResult(
        IReadOnlyList<Item> Items,
        IReadOnlyList<InventoryItem> InventoryItems
    );

    private record StartingItem(Item Item, int Quantity, EquipmentSlot? SlotOverride = null);
}
