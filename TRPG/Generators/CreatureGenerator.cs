using TRPG.Definitions;
using TRPG.Models;

namespace TRPG.Generators;

internal record CreatureGeneratorInput(
    CreatureType CreatureType,
    Profession Profession,
    Guid WorldId,
    Guid BirthStateId,
    Guid StateId,
    int Level = 0,
    string? Name = null);

internal record CreatureGeneratorResult(
    Creature Creature,
    IReadOnlyList<Item> Items,
    IReadOnlyList<InventoryItem> InventoryItems,
    IReadOnlyCollection<CreatureSkill> Skills,
    IReadOnlyCollection<CreatureAbility> Abilities);

internal class CreatureGenerator(ItemGenerator itemGenerator, AbilityDefinitions abilityDefinitions) {
    private static readonly NamePool HumanPool = new(
        [
            "Alden", "Alric", "Ansel", "Aric", "Baldric", "Bennet", "Beric", "Brand", "Brenner", "Cedric",
            "Corwin", "Darian", "Edric", "Edwin", "Eldric", "Emric", "Errol", "Falk", "Gareth", "Gavin",
            "Godric", "Hadrian", "Halric", "Harwin", "Jareth", "Kellan", "Leoric", "Lucan", "Merrick", "Osric",
            "Roderic", "Roland", "Stefan", "Theron", "Tristan", "Ulric", "Wulfric", "Alaric", "Bastian", "Calder"
        ],
        [
            "Adela", "Alena", "Anya", "Brienne", "Brynn", "Celia", "Clarice", "Delia", "Edith", "Elena",
            "Elise", "Evelyn", "Fiona", "Freya", "Gwen", "Helena", "Isolde", "Jocelyn", "Kaela", "Liora",
            "Lyanna", "Mara", "Maris", "Meriel", "Mirabel", "Nerys", "Roslyn", "Rowena", "Selene", "Serena",
            "Sylva", "Talia", "Thalia", "Valena", "Vera", "Vivienne", "Ysolde", "Arwen", "Catrin", "Maeve"
        ],
        [
            "Ashford", "Ashmere", "Blackwood", "Briar", "Brighton", "Coldbrook", "Dawnmere", "Dunridge",
            "Eastmere", "Fairchild", "Falconer", "Fenwick", "Frost", "Goldwell", "Graycastle", "Greymark",
            "Hawthorne", "Highmore", "Hillcrest", "Ironwood", "Kingsley", "Larkspur", "Longford", "Marwood",
            "Mournhill", "Northmere", "Oakheart", "Ravencrest", "Redbrook", "Riverstone",
            "Silverbrook", "Stonebridge", "Stormholt", "Thorne", "Valewood", "Westbrook", "Whitehill",
            "Wintermere", "Wolfhart", "Woodcroft"
        ]
    );

    private static readonly NamePool ElfPool = new(
        [
            "Aerendyl", "Aelar", "Arannis", "Caeleth", "Daenor", "Elaith", "Faelar", "Galadrian",
            "Laeroth", "Lorindel", "Rhovan", "Saelihn", "Selanar", "Silvyr", "Thamior", "Theren",
            "Vaeril", "Ylyndar", "Zeren", "Erevan", "Aramil"
        ],
        [
            "Althaea", "Arwen", "Eilistra", "Elenwe", "Elowen", "Ithilwen", "Kaelith", "Lethariel",
            "Lirael", "Maeralya", "Melian", "Miriel", "Naivara", "Nimrodel", "Nuala", "Shalana",
            "Sylvaris", "Sylwen", "Thalindra", "Vaelith", "Vanya"
        ],
        [
            "Amakiir", "Brightwater", "Dawnstrider", "Duskbrook", "Evenstar", "Goldenleaf",
            "Lightbringer", "Moonbrook", "Moonwhisper", "Nightbloom", "Silverbranch",
            "Silverleaf", "Starfall", "Starweaver", "Sunshadow", "Swiftbrook",
            "Whisperwind", "Willowmere", "Windrunner", "Winterbough", "Wyldwood",
            "Mistwalker", "Shadowmere", "Greenbriar", "Ashgrove"
        ]
    );

    private static readonly NamePool DwarfPool = new(
        [
            "Anrik", "Balgrim", "Bofri", "Borin", "Bruni", "Dain", "Dorin", "Dunric", "Durgan",
            "Eitri", "Grimnar", "Harbek", "Kadrin", "Morgran", "Nordri", "Orsik", "Rurik", "Sindri",
            "Thorgrim", "Thrain", "Thror", "Travok", "Ulfar", "Vondal", "Baldor", "Grundi", "Hargin", "Korin"
        ],
        [
            "Astrid", "Balina", "Dagnal", "Durga", "Freygund", "Helga", "Hilde", "Ketra",
            "Sigrun", "Torbera", "Vala", "Vistra", "Amber", "Artin", "Audhild", "Bardryn",
            "Diesa", "Eldeth", "Falkrunn", "Finellen", "Gunnloda", "Gurdis", "Helja", "Hlin",
            "Kathra", "Kristryd", "Liftrasa", "Torgga"
        ],
        [
            "Anvilfist", "Battleaxe", "Bronzebeard", "Coalbeard", "Deepdelver",
            "Emberforge", "Firemantle", "Forgehammer", "Goldfinder", "Granitehelm",
            "Grimforge", "Hammerfall", "Ironbeard", "Ironforge", "Ironjaw",
            "Rockfist", "Stonebeard", "Stonebreaker", "Stonehammer", "Steelheart",
            "Strongpick", "Deepstone", "Oreseeker", "Runehammer", "Blackanvil"
        ]
    );

    private static readonly NamePool OrcPool = new(
        [
            "Ghak", "Ghazbul", "Gorak", "Grukk", "Grul", "Karg", "Krusk", "Mogthar", "Morgash",
            "Skarn", "Thokk", "Throggar", "Ugluk", "Urgan", "Uzgash", "Uzarg", "Varg", "Vashak",
            "Vorak", "Vorn", "Yarg", "Zogar", "Zulgar", "Brug", "Grom", "Hruk", "Krosh", "Murg",
            "Torzug", "Urgash"
        ],
        [
            "Brakka", "Drakka", "Kagra", "Krenna", "Nagga", "Ruzka", "Ulzara", "Zagga", "Rakka", "Shura",
            "Baggi", "Emen", "Engong", "Kansif", "Myev", "Neega", "Ovak", "Ownka", "Shautha", "Sutha",
            "Vola", "Volen", "Yevelda", "Mogra", "Skarza", "Vorka", "Grazna", "Uzka", "Thura", "Zulga"
        ],
        [
            "Ashclaw", "Bloodfang", "Bonegnaw", "Bonecrusher", "Doomhowl",
            "Deathgrip", "Grimtusk", "Gutripper", "Ironhide", "Ragefist",
            "Rotmaw", "Skullcrusher", "Skullsplitter", "Skinflayer", "Spinebreaker",
            "Stormmaw", "The Butcher", "The Ravager", "Warscar", "Wolfkiller",
            "Blacktooth", "Redblade", "Fleshtearer", "Gravemaw", "Hellscar"
        ]
    );

    private static readonly NamePool HalflingPool = new(
        [
            "Alton", "Dudo", "Fenwick", "Finnan", "Fosco", "Hamish", "Hob", "Jasper", "Merri",
            "Milo", "Perrin", "Pip", "Samwise", "Tobias", "Wendell", "Roscoe", "Rollo", "Wilbur", "Hugo",
            "Ander", "Wellby"
        ],
        [
            "Bella", "Bramble", "Cora", "Daisy", "Elly", "Ivy", "Lavinia", "Lily", "Marigold", "Nora",
            "Poppy", "Primrose", "Rosalind", "Ruby", "Seraphina", "Willow", "Minta", "Posy", "Tansy",
            "Briony", "Esme"
        ],
        [
            "Applewood", "Appleford", "Berrybrook", "Brambleburr", "Brushgather",
            "Cobblestone", "Fairmeadow", "Goodbarrel", "Greenhill", "Hayworth",
            "Hilltopple", "Meadowlight", "Oakbottom", "Oakhollow", "Puddlefoot",
            "Riverburrow", "Softstep", "Thistledown", "Underbough", "Underhill",
            "Warmwater", "Whitethistle", "Honeyfoot", "Leafhopper", "Tealeaf"
        ]
    );

    private static readonly NamePool GnomePool = new(
        [
            "Alston", "Boddynock", "Bumblenoot", "Dimble", "Dobbin", "Fizzwick", "Gimble", "Jebeddo",
            "Nib", "Ordo", "Pipkin", "Sprocket", "Tink", "Tobin", "Widget", "Bramblewick", "Cog",
            "Fonkin", "Pog", "Rumpadump", "Tock", "Yaffle", "Zook"
        ],
        [
            "Bimble", "Frizzle", "Glimmerpop", "Mardnab", "Nooble", "Quibble", "Rill", "Tana",
            "Twyla", "Wizzle", "Zilly", "Dapple", "Ellywick", "Nissa", "Sivli", "Whim", "Zanna",
            "Bimpnottin", "Breena", "Donella", "Ella", "Nyx", "Oda"
        ],
        [
            "Bramblegear", "Bristlecog", "Cogsworth", "Copperwing", "Fidgetgear",
            "Fizzlewhistle", "Fumblebrass", "Geargrin", "Gearwhistle", "Nimblecog",
            "Pennywhistle", "Quickgear", "Rattlebolt", "Sparkspinner", "Sprocketstitch",
            "Tinkerbolt", "Togglegear", "Wobblegear", "Wondergear", "Whistlewick",
            "Clockspinner", "Steamcog", "Coppercog", "Gadgetgrin", "Boltspinner"
        ]
    );

    private static readonly string[] MonsterEpithets =
        ["Grim", "Foul", "Ancient", "Feral", "Ravenous", "Withered", "Nameless", "Restless"];

    private static readonly NamePool MonsterPool = new(
        MonsterEpithets,
        MonsterEpithets,
        ["Wraith", "Husk", "Fiend", "Beast", "Horror", "Shade", "Abomination", "Stalker"]
    );

    private static readonly Dictionary<CreatureType, NamePool[]> Pools = new() {
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
        [CreatureType.Elemental] = [MonsterPool]
    };

    private static readonly Dictionary<Profession, ArmorClass> ProfessionArmorClasses = new() {
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
        [Profession.Guard] = ArmorClass.Mail
    };

    private static readonly Dictionary<Profession, Skill[]> ProfessionSkills = new() {
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
        [Profession.Guard] = [Skill.Swordsmanship, Skill.Warfare]
    };

    private static readonly Dictionary<Profession, StatAffinities> Affinities = new() {
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
        [Profession.Guard] = new StatAffinities(2, 3, 1, 3, 1, 0, 0, 0.7f)
    };

    public CreatureGeneratorResult Generate(CreatureGeneratorInput generatorInput) {
        var level = generatorInput.Level > 0 ? generatorInput.Level : Random.Shared.Next(1, GameRules.MaxLevel + 1);
        var gender = Random.Shared.Next(2) == 0 ? Gender.Male : Gender.Female;

        var creature = new Creature {
            WorldId = generatorInput.WorldId,
            Name = generatorInput.Name ?? GetName(generatorInput.CreatureType, gender),
            CreatureType = generatorInput.CreatureType,
            Gender = gender,
            Profession = generatorInput.Profession,
            BirthStateId = generatorInput.BirthStateId,
            BirthYear = Random.Shared.Next(900, 975),
            Gold = GetGold(level, generatorInput.Profession),
            StateId = generatorInput.StateId,
            Attributes = GetAttributes(level, generatorInput.Profession),
            Level = level
        };

        var (items, inventoryItems) = GenerateStartingInventory(creature);
        var skills = GetSkills(creature);
        var abilities = GetAbilities(creature, skills);

        return new CreatureGeneratorResult(creature, items, inventoryItems, skills, abilities);
    }

    private static IReadOnlyCollection<CreatureSkill> GetSkills(Creature creature) {
        var skillLevel = Math.Min(creature.Level, GameRules.MaxSkillLevel);
        return ProfessionSkills[creature.Profession!.Value]
            .Select(skill => new CreatureSkill {
                CreatureId = creature.Id,
                Skill = skill,
                Level = skillLevel,
                Experience = GameRules.XpForSkillLevel(skillLevel),
                WorldId = creature.WorldId
            })
            .ToArray();
    }

    private IReadOnlyCollection<CreatureAbility> GetAbilities(Creature creature,
        IReadOnlyCollection<CreatureSkill> skills) {
        return skills
            .SelectMany(cs => abilityDefinitions.Abilities
                .Where(a => a.Skill == cs.Skill && a.RequiredSkillLevel <= cs.Level)
                .Select(a => new CreatureAbility
                    { CreatureId = creature.Id, AbilityName = a.Name, WorldId = creature.WorldId }))
            .ToArray();
    }

    private static int GetGold(int level, Profession profession) {
        var baseGold = level * 50;
        var spread = Random.Shared.Next((int) (baseGold * 0.8f), (int) (baseGold * 1.2f));
        return (int) (spread * Affinities[profession].GoldMultiplier);
    }

    private static Attributes GetAttributes(int level, Profession profession) {
        var a = Affinities[profession];
        int[] pool = [a.Strength, a.Defense, a.Dexterity, a.Endurance, a.Stamina, a.Mana, a.Intelligence];
        var total = pool.Sum();
        var stats = new int[7];
        Array.Fill(stats, 1);

        for (var i = 0; i < level * GameRules.PointsPerLevel; i++) {
            var roll = Random.Shared.Next(total);
            var cumulative = 0;
            for (var j = 0; j < pool.Length; j++) {
                cumulative += pool[j];
                if (roll < cumulative) {
                    stats[j]++;
                    break;
                }
            }
        }

        return new Attributes {
            Strength = stats[0],
            Defense = stats[1],
            Dexterity = stats[2],
            Endurance = stats[3],
            Stamina = stats[4],
            Mana = stats[5],
            Intelligence = stats[6],
            HpPercent = 1.0f,
            ApPercent = 1.0f,
            MpPercent = 1.0f
        };
    }

    private StartingInventoryResult GenerateStartingInventory(Creature creature) {
        var startingItems = GetStartingItems(creature);
        var items = new List<Item>();
        var inventoryItems = new List<InventoryItem>();
        var index = 0;

        foreach (var (item, quantity, slotOverride) in startingItems) {
            items.Add(item);
            inventoryItems.Add(new InventoryItem {
                CreatureId = creature.Id,
                ItemId = item.Id,
                Quantity = quantity,
                Index = index++,
                EquippedSlot = slotOverride ?? item.DefaultSlot,
                WorldId = creature.WorldId
            });
        }

        return new StartingInventoryResult(items.ToArray(), inventoryItems.ToArray());
    }

    private StartingItem[] GetStartingItems(Creature creature) {
        var level = creature.Level;
        var worldId = creature.WorldId;
        var armorClass = ProfessionArmorClasses.GetValueOrDefault(creature.Profession!.Value, ArmorClass.Leather);
        var armor = GetArmorItems(armorClass, level, worldId);
        var accessories = GetAccessoryItems(level, worldId);

        return creature.Profession switch {
            Profession.Knight => [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Sword, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateShield(level, worldId), 1),
                ..armor, ..accessories
            ],
            Profession.Rogue => [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Dagger, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Dagger, level, worldId), 1,
                    EquipmentSlot.LeftHand),
                ..armor, ..accessories
            ],
            Profession.Ranger => [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Bow, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateAmmo(AmmoType.Arrow, worldId), 20),
                ..armor, ..accessories
            ],
            Profession.Mage => [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Staff, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateConsumable(level, worldId), 3),
                ..armor, ..accessories
            ],
            Profession.Cleric => [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Mace, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateShield(level, worldId), 1),
                ..armor, ..accessories
            ],
            Profession.Mercenary => [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Sword, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateShield(level, worldId), 1),
                ..armor, ..accessories
            ],
            Profession.Alchemist => [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Wand, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateConsumable(level, worldId), 5),
                ..armor, ..accessories
            ],
            Profession.Blacksmith => [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Axe, level, worldId), 1),
                ..armor, ..accessories
            ],
            Profession.Scholar => [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Staff, level, worldId), 1),
                ..armor, ..accessories
            ],
            _ => [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Dagger, level, worldId), 1),
                ..armor, ..accessories
            ]
        };
    }

    private StartingItem[] GetArmorItems(ArmorClass armorClass, int level, Guid worldId) {
        return [
            new StartingItem(itemGenerator.GenerateArmor(ArmorType.Helm, armorClass, level, worldId), 1),
            new StartingItem(itemGenerator.GenerateArmor(ArmorType.Chest, armorClass, level, worldId), 1),
            new StartingItem(itemGenerator.GenerateArmor(ArmorType.Gloves, armorClass, level, worldId), 1),
            new StartingItem(itemGenerator.GenerateArmor(ArmorType.Boots, armorClass, level, worldId), 1)
        ];
    }

    private StartingItem[] GetAccessoryItems(int level, Guid worldId) {
        return [
            new StartingItem(itemGenerator.GenerateAccessory(AccessoryType.Necklace, level, worldId), 1),
            new StartingItem(itemGenerator.GenerateAccessory(AccessoryType.Belt, level, worldId), 1),
            new StartingItem(itemGenerator.GenerateAccessory(AccessoryType.Ring, level, worldId), 1),
            new StartingItem(itemGenerator.GenerateAccessory(AccessoryType.Ring, level, worldId), 1,
                EquipmentSlot.RightRing)
        ];
    }

    public static string GetName(CreatureType creatureType, Gender gender) {
        var pools = Pools.GetValueOrDefault(creatureType, [MonsterPool]);
        var pool = pools[Random.Shared.Next(pools.Length)];
        var firstNames = gender == Gender.Male ? pool.MaleFirstNames : pool.FemaleFirstNames;
        var first = firstNames[Random.Shared.Next(firstNames.Length)];
        var last = pool.LastNames[Random.Shared.Next(pool.LastNames.Length)];
        return $"{first} {last}";
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

    private record StartingInventoryResult(IReadOnlyList<Item> Items, IReadOnlyList<InventoryItem> InventoryItems);

    private record StartingItem(Item Item, int Quantity, EquipmentSlot? SlotOverride = null);
}
