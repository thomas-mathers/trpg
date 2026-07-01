using TRPG.Definitions;
using TRPG.Models;

namespace TRPG.Generators;

internal record PersonGeneratorInput(Race Race, Profession Profession, Guid WorldId, Guid BirthRegionId, Location Location, int Level = 0, string? Name = null);

internal record PersonGeneratorResult(
    Person Person,
    IReadOnlyList<Item> Items,
    IReadOnlyList<InventoryItem> InventoryItems,
    IReadOnlyCollection<PersonSkill> Skills,
    IReadOnlyCollection<PersonAbility> Abilities);

internal class PersonGenerator(ItemGenerator itemGenerator, AbilityDefinitions abilityDefinitions) {
    private static readonly Dictionary<string, NamePool> Pools = new(StringComparer.OrdinalIgnoreCase) {
        ["Nordic/Viking"] = new NamePool(
            [
                "Einar", "Sigrun", "Thorvald", "Ragnhild", "Bjorn", "Ingrid", "Leif", "Astrid", "Gunnar", "Freydis",
                "Ulf", "Helga", "Ragnar", "Solveig", "Harald", "Gudrun", "Ivar", "Thyra", "Ketil", "Ylva",
                "Erik", "Sigrid", "Arne", "Hilda", "Olaf", "Ragna", "Hakon", "Sif", "Halfdan", "Vigdis",
                "Torstein", "Bergljot", "Frode", "Borghild", "Audun", "Svein", "Ingebjorg", "Rolf", "Hervor", "Tora"
            ],
            [
                "Eriksson", "Sigurdsson", "Bjornsen", "Geirssen", "Ragnvarsson", "Thorsson", "Halvardsen", "Ulfsson",
                "Leifsson", "Gunnarsson", "Haraldssen", "Ivarsson", "Ketilsson", "Sveinsson", "Magnusson", "Olafsson",
                "Grimsson", "Asbjornsen", "Ormsson", "Baldursson",
                "Arnesson", "Hakonsson", "Egilsson", "Torvaldsson", "Rolfsson", "Torbjornsen", "Styrsson", "Vigfusson",
                "Brandsson", "Thormodsson", "Herjolfsson", "Oddsson", "Knutsson", "Svendsen", "Nilsson", "Karlsson",
                "Sigvaldsson", "Bergthorsson", "Valgardsson", "Grimmsson"
            ]
        ),
        ["Roman"] = new NamePool(
            [
                "Gaius", "Lucia", "Marcus", "Valeria", "Quintus", "Iulia", "Decimus", "Claudia", "Titus", "Livia",
                "Lucius", "Cornelia", "Publius", "Aurelia", "Aulus", "Fabia", "Manius", "Servilia", "Gnaeus", "Clodia",
                "Brutus", "Porcia", "Sextus", "Minucia", "Appius", "Fulvia", "Spurius", "Mucia", "Kaeso", "Caecilia",
                "Vibius", "Tertia", "Opiter", "Laelia", "Numerius", "Postumia", "Statius", "Horatia", "Caius", "Flavia"
            ],
            [
                "Aurelius", "Caecilius", "Flavius", "Valerius", "Cornelius", "Fabius", "Iulius", "Claudius", "Pompeius",
                "Cassius", "Licinius", "Sempronius", "Calpurnius", "Hortensius", "Marius", "Servilius", "Plautius",
                "Domitius", "Gracchus", "Papirius",
                "Aemilius", "Atilius", "Furius", "Hostilius", "Junius", "Livius", "Manlius", "Naevius", "Oppius",
                "Petronius", "Quinctius", "Scipio", "Tullius", "Vergilius", "Volumnius", "Crassus", "Plinius",
                "Tacitus", "Lucretius", "Marcellus"
            ]
        ),
        ["Feudal Japanese"] = new NamePool(
            [
                "Aoi", "Hikaru", "Kazuki", "Ryota", "Sora", "Yui", "Haruto", "Natsuki", "Miyu", "Shizuka", "Tsubasa",
                "Akira", "Kotaro", "Eriko", "Fumito", "Megumi", "Rei", "Satoko", "Takashi", "Yuuta",
                "Ichiro", "Hanako", "Kenji", "Sakura", "Tadashi", "Noriko", "Hiroshi", "Emi", "Katsuro", "Yuki",
                "Sosuke", "Asuka", "Daisuke", "Chie", "Masaru", "Tomoko", "Noboru", "Fumiko", "Ryu", "Hana"
            ],
            [
                "Tanaka", "Fujiwara", "Nakamura", "Yamamoto", "Watanabe", "Kobayashi", "Saito", "Kato", "Suzuki",
                "Sasaki", "Hashimoto", "Inoue", "Kimura", "Hayashi", "Shimizu", "Yamaguchi", "Mori", "Abe", "Ikeda",
                "Matsumoto",
                "Ogawa", "Ishikawa", "Yamada", "Nakagawa", "Takahashi", "Nishimura", "Ito", "Kondo", "Goto", "Fujita",
                "Okamoto", "Aoki", "Nagai", "Ishida", "Ueda", "Kawaguchi", "Ono", "Fujii", "Okada", "Maeda"
            ]
        ),
        ["Arabic/Persian"] = new NamePool(
            [
                "Salam", "Faris", "Nizar", "Malik", "Zayd", "Hassan", "Amir", "Ibrahim", "Tariq", "Yasmin", "Layla",
                "Farid", "Khalid", "Samir", "Rania", "Zafar", "Nadia", "Rashid", "Leila", "Karim",
                "Ahmad", "Fatima", "Ali", "Mariam", "Omar", "Soraya", "Darius", "Shirin", "Cyrus", "Parisa",
                "Rostam", "Zara", "Yusuf", "Samira", "Babak", "Golnaz", "Siavash", "Nasrin", "Huda", "Basim"
            ],
            [
                "Rashid", "Hassan", "Khalid", "Mansur", "Aziz", "Nasser", "Sharif", "Habib", "Amin", "Jabir", "Kindi",
                "Biruni", "Zahrani", "Tamimi", "Saleh", "Masri", "Omar", "Yusuf", "Suleiman", "Farsi",
                "Al-Amin", "Al-Hakim", "Al-Nasir", "Qureshi", "Tahir", "Hamdan", "Badr", "Zaidi", "Shirazi",
                "Tehrani", "Khorasani", "Isfahani", "Gilani", "Yazdi", "Tabari", "Bukhari", "Razi", "Tusi",
                "Kashani", "Shirwani"
            ]
        ),
        ["Celtic"] = new NamePool(
            [
                "Aodhan", "Branwen", "Caolán", "Dairíne", "Eilís", "Fínghin", "Gabhraen", "Iarlaith", "Maeve", "Niamh",
                "Órlaith", "Pádraic", "Ruadhán", "Sorcha", "Tiarnán", "Úna", "Ciarán", "Bríd", "Eoghan", "Llŷr",
                "Aisling", "Conall", "Deirdre", "Fergus", "Grainne", "Lugh", "Muirenn", "Oisin", "Ronan", "Saoirse",
                "Tadhg", "Aoife", "Fintan", "Doireann", "Lorcan", "Treasa", "Colm", "Sadhbh", "Fionn", "Caoimhe"
            ],
            [
                "MacCormac", "O'Brien", "MacFhinn", "O'Donnell", "MacEochaid", "O'Connell", "MacNiall", "MacBrennan",
                "O'Sullivan", "MacRuaidh", "O'Dwyer", "MacGorman", "O'Shea", "MacCarthy", "O'Mara", "MacBrody",
                "O'Faolain", "MacQuillian", "O'Callaghan", "MacGilla",
                "MacCullough", "O'Flynn", "MacKeown", "O'Riordan", "MacNamara", "O'Higgins", "MacTiernan", "O'Neill",
                "MacLysaght", "O'Leary", "MacCabe", "O'Toole", "MacFarlane", "O'Grady", "MacLoughlin", "O'Mahony",
                "MacAuliffe", "O'Donoghue", "MacLochlainn", "O'Donovan"
            ]
        ),
        ["Slavic"] = new NamePool(
            [
                "Ivan", "Miroslav", "Nikita", "Vladimir", "Dmitry", "Sergei", "Pavel", "Olga", "Natasha", "Katerina",
                "Aleksandr", "Boris", "Yuri", "Andrei", "Mikhail", "Nadia", "Zoya", "Vasily", "Igor", "Vera",
                "Pyotr", "Tatiana", "Nikolai", "Oksana", "Fedor", "Lyudmila", "Grigory", "Sofia", "Alexei", "Darya",
                "Stanislav", "Yelena", "Konstantin", "Polina", "Ruslan", "Svetlana", "Timur", "Varvara", "Lev", "Arina"
            ],
            [
                "Petrov", "Volkov", "Sokolov", "Morozov", "Lebedev", "Kozlov", "Popov", "Sobolev", "Makarov", "Nikitin",
                "Orlov", "Zhukov", "Vinogradov", "Stepanov", "Kovalev", "Borisov", "Fedorov", "Ryabov", "Baranov",
                "Novak",
                "Smirnov", "Ivanov", "Kuznetsov", "Vasilev", "Mikhailov", "Alekseev", "Yakovlev", "Andreev",
                "Grigoryev", "Pavlov", "Semyonov", "Golubev", "Kuzmin", "Kulikov", "Titov", "Medvedev", "Frolov",
                "Zaytsev", "Belousov", "Chernov"
            ]
        ),
        ["Ancient Egyptian"] = new NamePool(
            [
                "Amenemhat", "Nefertiti", "Meryt", "Yuya", "Khnumhotep", "Senenmut", "Ptahmose", "Nefermaat",
                "Meritamun", "Wenis", "Nakhtmin", "Pashedu", "Qenena", "Renpet", "Minnefer", "Amunhotep", "Nebwenenef",
                "Rahotep", "Sitamun", "Thutmose",
                "Horemheb", "Neferu", "Useramon", "Taweret", "Menkheperre", "Mutnodjmet", "Ankhesenamun", "Nefertari",
                "Setnakht", "Tausret", "Nebhepetre", "Ahhotep", "Merneptah", "Nebkaure", "Sekhemkhet", "Djedefre",
                "Khafre", "Menkaure", "Sneferu", "Ay"
            ],
            [
                "Amenemope", "Heqareshu", "Nebnetjeru", "Userhat", "Nebamun", "Sennefer", "Djehuty", "Nebpehtyre",
                "Henut", "Ipet", "Mahu", "Kenamun", "Harkhuf", "Huy", "Pentu", "Kiya", "Tiye", "Mehy", "Ani", "Nebseni",
                "Paheri", "Djedefhor", "Useramen", "Kahep", "Neferrenpet", "Amunwosret", "Bakamun", "Sanakht",
                "Nebra", "Khendjer", "Neferhotep", "Sobekhotep", "Intefoker", "Dagi", "Wah", "Montuemhat",
                "Nespakashuty", "Khaemwaset", "Ramose", "Hunefer"
            ]
        ),
        ["Mesoamerican"] = new NamePool(
            [
                "Xochitl", "Quetzalli", "Nopaltzin", "Coyotl", "Ixtlixochitl", "Papatzin", "Chimalman", "Citlali",
                "Itzli", "Cuauhtli", "Huitzilin", "Coyohua", "Tlalli", "Yaotl", "Mazatl", "Necahual", "Tezozomoc",
                "Ixcuiname", "Chalchiuh", "Acamapich",
                "Atototl", "Cuetlaxochitl", "Iztac", "Ehecatl", "Miquiztli", "Xipe", "Tecpatl", "Tochtli", "Tonatiuh",
                "Metztli", "Quiahuitl", "Ozomatl", "Olin", "Malinalli", "Ocelotl", "Cipactli", "Tlacaelel", "Itzcoatl",
                "Axayacatl", "Tizoc"
            ],
            [
                "Chimalli", "Tlamani", "Teopan", "Coatlan", "Atlapal", "Tlalcoatl", "Atzaqualco", "Moyotlan",
                "Cuepopan", "Xoloco", "Tepetzinco", "Mexicapan", "Tlatilco", "Copilco", "Acatlan", "Olinco",
                "Xochitlan", "Quauhtlan", "Mazatlan", "Cipactlan",
                "Nonoalco", "Tenochtla", "Azcapan", "Texcocan", "Tlacopan", "Huitzilla", "Atzompa", "Tepeaca",
                "Cholola", "Cempoala", "Tuxpanec", "Tamoanca", "Tlaltica", "Tolleca", "Culhua", "Colhua", "Tlaxcala",
                "Quetzalpa", "Michhua", "Matlatzinca"
            ]
        ),
        ["Byzantine"] = new NamePool(
            [
                "Alexios", "Theophilos", "Dimitrios", "Nikolaos", "Kallistratos", "Eirene", "Athenodoros", "Photios",
                "Leontios", "Panteleimon", "Kyriakos", "Hypatia", "Makarios", "Zoe", "Theoktiste", "Nereida",
                "Philaretos", "Sophronios", "Kalliope", "Anastasios",
                "Basileios", "Konstantinos", "Ioannes", "Georgios", "Theodoros", "Maria", "Anna", "Helena", "Theodora",
                "Euphrosyne", "Manuel", "Andronikos", "Nikephoros", "Romanos", "Symeon", "Athanasios", "Eudokia",
                "Christophoros", "Xene", "Sabbas"
            ],
            [
                "Komnenos", "Palaiologos", "Doukas", "Botaneiates", "Phokas", "Melissenos", "Angelos",
                "Philanthropenos", "Laskaris", "Monomachos", "Tzimiskes", "Argyros", "Dalassenos", "Tornikes",
                "Katakalon", "Kontostephanos", "Vatatzes", "Kantakouzenos", "Skleros", "Bryennios",
                "Diogenes", "Tarchaneiotes", "Strategopoulos", "Gabras", "Gabalas", "Apokaukos", "Metochites",
                "Choniates", "Attaleiates", "Kekaumenos", "Skylitzes", "Kinnamos", "Pachymeres", "Gregoras",
                "Zonaras", "Manasses", "Sphrantzes", "Chrysoberges", "Maniakes", "Batatzes"
            ]
        ),
        ["Mongol"] = new NamePool(
            [
                "Temujin", "Borte", "Jebe", "Subutai", "Chagatai", "Ogodei", "Tolui", "Hulagu", "Berke", "Arghun",
                "Ghazan", "Timur", "Nogai", "Toqta", "Ozbek", "Jochi", "Sorqaqtani", "Toregene", "Chabi", "Bortei",
                "Kublai", "Mongke", "Guyuk", "Batu", "Orda", "Shiban", "Nayan", "Kaidu", "Duwa", "Kebek",
                "Yesugei", "Hoelun", "Qutulun", "Abish", "Altun", "Mandughai", "Eljigidei", "Tangut", "Bayan", "Muqali"
            ],
            [
                "Borjigin", "Merkit", "Naiman", "Kereit", "Jalayir", "Oirat", "Khongirad", "Barlas", "Suldus", "Besut",
                "Bayaut", "Taichiut", "Dorbet", "Manghud", "Ongirat", "Ikires", "Baya'ud", "Uru'ud", "Noyad",
                "Tayichi'ud",
                "Qatagin", "Saljiut", "Jadaran", "Arulad", "Uriankhai", "Khatagin", "Khuushiin", "Tumad", "Ongud",
                "Baarin", "Buryat", "Khalkha", "Khorlod", "Choros", "Dorbod", "Oyirad", "Jalairs", "Qarait",
                "Kheshig", "Negus"
            ]
        )
    };

    private static readonly NamePool FallbackNamePool = new(
        [
            "Aldric", "Brenna", "Caelan", "Dorin", "Elowen", "Faren", "Gorin", "Halia", "Idris", "Jorah", "Kael",
            "Lyra", "Maren", "Niran", "Orin", "Pira", "Quill", "Rael", "Soren", "Talia",
            "Aldwyn", "Bryn", "Calder", "Davan", "Elara", "Faelan", "Gareth", "Haela", "Isran", "Jorin",
            "Kiran", "Lira", "Mira", "Nael", "Oryn", "Petra", "Quillan", "Riven", "Syra", "Talon"
        ],
        [
            "Ashvale", "Blackmere", "Coldwyn", "Duskmore", "Elmvale", "Frostholm", "Greymoor", "Harwick", "Ironfeld",
            "Jaedwyn", "Keldmere", "Larkmoor", "Mistfall", "Northvale", "Oakfen", "Pinegard", "Quarrymere", "Ravenmoor",
            "Stonegard", "Thornmere",
            "Ambervale", "Brightfen", "Crestholm", "Deepwater", "Edgewick", "Fallowhurst", "Goldenfield", "Highmark",
            "Ivywood", "Jadecrest", "Kindlewood", "Lowmoor", "Marshwick", "Nightvale", "Oldfield", "Peakford",
            "Redholm", "Silverfen", "Timberwood", "Wolfmere"
        ]
    );

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
        [Profession.Guard] = ArmorClass.Mail,
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

    public PersonGeneratorResult Generate(PersonGeneratorInput generatorInput) {
        var level = generatorInput.Level > 0 ? generatorInput.Level : Random.Shared.Next(1, GameRules.MaxLevel + 1);

        var person = new Person {
            WorldId = generatorInput.WorldId,
            Name = generatorInput.Name ?? GetName(generatorInput.Race.CultureStyle),
            RaceId = generatorInput.Race.Id,
            Profession = generatorInput.Profession,
            BirthRegionId = generatorInput.BirthRegionId,
            BirthYear = Random.Shared.Next(900, 975),
            Gold = GetGold(level, generatorInput.Profession),
            Location = generatorInput.Location,
            Attributes = GetAttributes(level, generatorInput.Profession),
            Level = level
        };

        var (items, inventoryItems) = GenerateStartingInventory(person);
        var skills = GetSkills(person);
        var abilities = GetAbilities(person, skills);

        return new PersonGeneratorResult(person, items, inventoryItems, skills, abilities);
    }

    private static IReadOnlyCollection<PersonSkill> GetSkills(Person person) {
        var skillLevel = Math.Min(person.Level, GameRules.MaxSkillLevel);
        return ProfessionSkills[person.Profession]
            .Select(skill => new PersonSkill {
                PersonId = person.Id,
                Skill = skill,
                Level = skillLevel,
                Experience = GameRules.XpForSkillLevel(skillLevel)
            })
            .ToArray();
    }

    private IReadOnlyCollection<PersonAbility> GetAbilities(Person person, IReadOnlyCollection<PersonSkill> skills) {
        return skills
            .SelectMany(ps => abilityDefinitions.Abilities
                .Where(a => a.Skill == ps.Skill && a.RequiredSkillLevel <= ps.Level)
                .Select(a => new PersonAbility { PersonId = person.Id, AbilityName = a.Name }))
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

    private StartingInventoryResult GenerateStartingInventory(Person person) {
        var startingItems = GetStartingItems(person);
        var items = new List<Item>();
        var inventoryItems = new List<InventoryItem>();
        var index = 0;

        foreach (var (item, quantity, slotOverride) in startingItems) {
            items.Add(item);
            inventoryItems.Add(new InventoryItem {
                PersonId = person.Id,
                ItemId = item.Id,
                Quantity = quantity,
                Index = index++,
                EquippedSlot = slotOverride ?? item.DefaultSlot
            });
        }

        return new StartingInventoryResult(items.ToArray(), inventoryItems.ToArray());
    }

    private StartingItem[] GetStartingItems(Person person) {
        var level = person.Level;
        var worldId = person.WorldId;
        var armorClass = ProfessionArmorClasses.GetValueOrDefault(person.Profession, ArmorClass.Leather);
        var armor = GetArmorItems(armorClass, level, worldId);
        var accessories = GetAccessoryItems(level, worldId);

        return person.Profession switch {
            Profession.Knight => [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Sword, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateShield(level, worldId), 1),
                ..armor, ..accessories
            ],
            Profession.Rogue => [
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Dagger, level, worldId), 1),
                new StartingItem(itemGenerator.GenerateWeapon(WeaponType.Dagger, level, worldId), 1, EquipmentSlot.LeftHand),
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

    private StartingItem[] GetArmorItems(ArmorClass armorClass, int level, Guid worldId) => [
        new StartingItem(itemGenerator.GenerateArmor(ArmorType.Helm, armorClass, level, worldId), 1),
        new StartingItem(itemGenerator.GenerateArmor(ArmorType.Chest, armorClass, level, worldId), 1),
        new StartingItem(itemGenerator.GenerateArmor(ArmorType.Gloves, armorClass, level, worldId), 1),
        new StartingItem(itemGenerator.GenerateArmor(ArmorType.Boots, armorClass, level, worldId), 1),
    ];

    private StartingItem[] GetAccessoryItems(int level, Guid worldId) => [
        new StartingItem(itemGenerator.GenerateAccessory(AccessoryType.Necklace, level, worldId), 1),
        new StartingItem(itemGenerator.GenerateAccessory(AccessoryType.Belt, level, worldId), 1),
        new StartingItem(itemGenerator.GenerateAccessory(AccessoryType.Ring, level, worldId), 1),
        new StartingItem(itemGenerator.GenerateAccessory(AccessoryType.Ring, level, worldId), 1, EquipmentSlot.RightRing),
    ];

    public static string GetName(string cultureStyle) {
        var pool = Pools.GetValueOrDefault(cultureStyle, FallbackNamePool);
        var first = pool.FirstNames[Random.Shared.Next(pool.FirstNames.Length)];
        var last = pool.LastNames[Random.Shared.Next(pool.LastNames.Length)];
        return $"{first} {last}";
    }

    private record NamePool(string[] FirstNames, string[] LastNames);

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
