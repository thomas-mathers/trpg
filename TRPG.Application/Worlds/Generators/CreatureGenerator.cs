using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Common.Algorithms;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Inventory;
using TRPG.Contracts.Creatures.Requests;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public record CreatureGeneratorInput(
    CreatureType CreatureType,
    CreatureArchetype Archetype,
    Guid WorldId,
    Guid BirthStateId,
    Guid StateId,
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
    IReadOnlyCollection<CreatureSkill> Skills,
    IReadOnlyCollection<CreatureAbility> Abilities
);

public class CreatureGenerator(
    ItemGenerator itemGenerator,
    AbilityDefinitions abilityDefinitions,
    IOptionsSnapshot<CreatureGeneratorOptions> optionsSnapshot,
    StatFormulas statFormulas
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

    public CreatureGeneratorResult Generate(CreatureGeneratorInput generatorInput)
    {
        var archetype = generatorInput.Archetype;
        var level = Random.Shared.Next(generatorInput.MinLevel, generatorInput.MaxLevel + 1);

        var gender =
            generatorInput.Gender ?? Random.Shared.GetItems(Enum.GetValues<Gender>(), 1).First();

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
            BirthStateId = generatorInput.BirthStateId,
            BirthYear = Random.Shared.Next(
                generatorInput.MinBirthYear ?? 900,
                generatorInput.MaxBirthYear ?? 975
            ),
            Gold = GetGold(level, archetype),
            StateId = generatorInput.StateId,
            BaseAttributes = attributes,
            LastRegenPlaytime = TimeSpan.Zero,
            Level = level,
        };

        var items = GenerateStartingInventory(creature, archetype).ToList();
        if (creature.Gold > 0)
        {
            items.Add(
                new Gold
                {
                    WorldId = creature.WorldId,
                    Name = "Gold",
                    Quantity = creature.Gold,
                    Ownership = new ItemOwnership
                    {
                        OwnerId = creature.Id,
                        OwnerType = OwnerType.Creature,
                    },
                }
            );
        }
        var equippedItems = items.Where(item => item.Ownership.EquippedSlot != null).ToArray();

        CreatureAttributesRecalculator.Recalculate(creature, equippedItems);

        creature.CurrentHp = creature.MaximumHp;
        creature.CurrentAp = creature.MaximumAp;
        creature.CurrentMp = creature.MaximumMp;

        var skills = CreatureSkillsGenerator.Generate(creature, archetype.SkillAffinities);

        var abilities = GetAbilities(creature, skills);

        return new CreatureGeneratorResult(creature, items, skills, abilities);
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

    private static int GetGold(int level, CreatureArchetype archetype)
    {
        var baseGold = level * 50;
        var spread = Random.Shared.Next((int)(baseGold * 0.8f), (int)(baseGold * 1.2f));
        return (int)(spread * archetype.StatAffinities.GoldMultiplier);
    }

    private Attributes GetAttributes(int level, CreatureArchetype archetype)
    {
        var a = archetype.StatAffinities;
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
        var baseline = optionsSnapshot.Value.BaseAttributes;
        int[] stats = [1, 1, 1, 1, 1, 1, 1];

        var draws = baseline.Total() - stats.Length + level * optionsSnapshot.Value.PointsPerLevel;
        for (var i = 0; i < draws; i++)
        {
            stats[WeightedSampler.SampleIndex(pool)]++;
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
            MaximumHp = statFormulas.CalculateMaximumHp(baseAttributes),
            MaximumAp = statFormulas.CalculateMaximumAp(baseAttributes),
            MaximumMp = statFormulas.CalculateMaximumMp(baseAttributes),
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
            MaximumHp = statFormulas.CalculateMaximumHp(baseAttributes),
            MaximumAp = statFormulas.CalculateMaximumAp(baseAttributes),
            MaximumMp = statFormulas.CalculateMaximumMp(baseAttributes),
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

            var resolvedSlot = slotOverride ?? ItemEquipmentPolicy.GetDefaultSlot(item);
            if (resolvedSlot != null && occupiedSlots.Add(resolvedSlot.Value))
            {
                item.Ownership.EquippedSlot = resolvedSlot;
            }

            items.Add(item);
        }

        return items;
    }

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
        var pools = Pools.GetValueOrDefault(creatureType, [MonsterPool]);
        return pools[Random.Shared.Next(pools.Length)];
    }

    private record NamePool(string[] MaleFirstNames, string[] FemaleFirstNames, string[] LastNames);

    private record StartingItem(Item Item, int Quantity, EquipmentSlot? SlotOverride = null);
}
