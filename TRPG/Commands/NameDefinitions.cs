namespace TRPG.Commands;

internal static class NameDefinitions {
    private static readonly Dictionary<string, Pool> Pools = new(StringComparer.OrdinalIgnoreCase) {
        ["Nordic/Viking"] = new Pool(
            [
                "Einar", "Sigrun", "Thorvald", "Ragnhild", "Bjorn", "Ingrid", "Leif", "Astrid", "Gunnar", "Freydis",
                "Ulf", "Helga", "Ragnar", "Solveig", "Harald", "Gudrun", "Ivar", "Thyra", "Ketil", "Ylva"
            ],
            [
                "Eriksson", "Sigurdsson", "Bjornsen", "Geirssen", "Ragnvarsson", "Thorsson", "Halvardsen", "Ulfsson",
                "Leifsson", "Gunnarsson", "Haraldssen", "Ivarsson", "Ketilsson", "Sveinsson", "Magnusson", "Olafsson",
                "Grimsson", "Asbjornsen", "Ormsson", "Baldursson"
            ]
        ),
        ["Roman"] = new Pool(
            [
                "Gaius", "Lucia", "Marcus", "Valeria", "Quintus", "Iulia", "Decimus", "Claudia", "Titus", "Livia",
                "Lucius", "Cornelia", "Publius", "Aurelia", "Aulus", "Fabia", "Manius", "Servilia", "Gnaeus", "Clodia"
            ],
            [
                "Aurelius", "Caecilius", "Flavius", "Valerius", "Cornelius", "Fabius", "Iulius", "Claudius", "Pompeius",
                "Cassius", "Licinius", "Sempronius", "Calpurnius", "Hortensius", "Marius", "Servilius", "Plautius",
                "Domitius", "Gracchus", "Papirius"
            ]
        ),
        ["Feudal Japanese"] = new Pool(
            [
                "Aoi", "Hikaru", "Kazuki", "Ryota", "Sora", "Yui", "Haruto", "Natsuki", "Miyu", "Shizuka", "Tsubasa",
                "Akira", "Kotaro", "Eriko", "Fumito", "Megumi", "Rei", "Satoko", "Takashi", "Yuuta"
            ],
            [
                "Tanaka", "Fujiwara", "Nakamura", "Yamamoto", "Watanabe", "Kobayashi", "Saito", "Kato", "Suzuki",
                "Sasaki", "Hashimoto", "Inoue", "Kimura", "Hayashi", "Shimizu", "Yamaguchi", "Mori", "Abe", "Ikeda",
                "Matsumoto"
            ]
        ),
        ["Arabic/Persian"] = new Pool(
            [
                "Salam", "Faris", "Nizar", "Malik", "Zayd", "Hassan", "Amir", "Ibrahim", "Tariq", "Yasmin", "Layla",
                "Farid", "Khalid", "Samir", "Rania", "Zafar", "Nadia", "Rashid", "Leila", "Karim"
            ],
            [
                "Rashid", "Hassan", "Khalid", "Mansur", "Aziz", "Nasser", "Sharif", "Habib", "Amin", "Jabir", "Kindi",
                "Biruni", "Zahrani", "Tamimi", "Saleh", "Masri", "Omar", "Yusuf", "Suleiman", "Farsi"
            ]
        ),
        ["Celtic"] = new Pool(
            [
                "Aodhan", "Branwen", "Caolán", "Dairíne", "Eilís", "Fínghin", "Gabhraen", "Iarlaith", "Maeve", "Niamh",
                "Órlaith", "Pádraic", "Ruadhán", "Sorcha", "Tiarnán", "Úna", "Ciarán", "Bríd", "Eoghan", "Llŷr"
            ],
            [
                "MacCormac", "O'Brien", "MacFhinn", "O'Donnell", "MacEochaid", "O'Connell", "MacNiall", "MacBrennan",
                "O'Sullivan", "MacRuaidh", "O'Dwyer", "MacGorman", "O'Shea", "MacCarthy", "O'Mara", "MacBrody",
                "O'Faolain", "MacQuillian", "O'Callaghan", "MacGilla"
            ]
        ),
        ["Slavic"] = new Pool(
            [
                "Ivan", "Miroslav", "Nikita", "Vladimir", "Dmitry", "Sergei", "Pavel", "Olga", "Natasha", "Katerina",
                "Aleksandr", "Boris", "Yuri", "Andrei", "Mikhail", "Nadia", "Zoya", "Vasily", "Igor", "Vera"
            ],
            [
                "Petrov", "Volkov", "Sokolov", "Morozov", "Lebedev", "Kozlov", "Popov", "Sobolev", "Makarov", "Nikitin",
                "Orlov", "Zhukov", "Vinogradov", "Stepanov", "Kovalev", "Borisov", "Fedorov", "Ryabov", "Baranov",
                "Novak"
            ]
        ),
        ["Ancient Egyptian"] = new Pool(
            [
                "Amenemhat", "Nefertiti", "Meryt", "Yuya", "Khnumhotep", "Senenmut", "Ptahmose", "Nefermaat",
                "Meritamun", "Wenis", "Nakhtmin", "Pashedu", "Qenena", "Renpet", "Minnefer", "Amunhotep", "Nebwenenef",
                "Rahotep", "Sitamun", "Thutmose"
            ],
            [
                "Amenemope", "Heqareshu", "Nebnetjeru", "Userhat", "Nebamun", "Sennefer", "Djehuty", "Nebpehtyre",
                "Henut", "Ipet", "Mahu", "Kenamun", "Harkhuf", "Huy", "Pentu", "Kiya", "Tiye", "Mehy", "Ani", "Nebseni"
            ]
        ),
        ["Mesoamerican"] = new Pool(
            [
                "Xochitl", "Quetzalli", "Nopaltzin", "Coyotl", "Ixtlixochitl", "Papatzin", "Chimalman", "Citlali",
                "Itzli", "Cuauhtli", "Huitzilin", "Coyohua", "Tlalli", "Yaotl", "Mazatl", "Necahual", "Tezozomoc",
                "Ixcuiname", "Chalchiuh", "Acamapich"
            ],
            [
                "Chimalli", "Tlamani", "Teopan", "Coatlan", "Atlapal", "Tlalcoatl", "Atzaqualco", "Moyotlan",
                "Cuepopan", "Xoloco", "Tepetzinco", "Mexicapan", "Tlatilco", "Copilco", "Acatlan", "Olinco",
                "Xochitlan", "Quauhtlan", "Mazatlan", "Cipactlan"
            ]
        ),
        ["Byzantine"] = new Pool(
            [
                "Alexios", "Theophilos", "Dimitrios", "Nikolaos", "Kallistratos", "Eirene", "Athenodoros", "Photios",
                "Leontios", "Panteleimon", "Kyriakos", "Hypatia", "Makarios", "Zoe", "Theoktiste", "Nereida",
                "Philaretos", "Sophronios", "Kalliope", "Anastasios"
            ],
            [
                "Komnenos", "Palaiologos", "Doukas", "Botaneiates", "Phokas", "Melissenos", "Angelos",
                "Philanthropenos", "Laskaris", "Monomachos", "Tzimiskes", "Argyros", "Dalassenos", "Tornikes",
                "Katakalon", "Kontostephanos", "Vatatzes", "Kantakouzenos", "Skleros", "Bryennios"
            ]
        ),
        ["Mongol"] = new Pool(
            [
                "Temujin", "Borte", "Jebe", "Subutai", "Chagatai", "Ogodei", "Tolui", "Hulagu", "Berke", "Arghun",
                "Ghazan", "Timur", "Nogai", "Toqta", "Ozbek", "Jochi", "Sorqaqtani", "Toregene", "Chabi", "Bortei"
            ],
            [
                "Borjigin", "Merkit", "Naiman", "Kereit", "Jalayir", "Oirat", "Khongirad", "Barlas", "Suldus", "Besut",
                "Bayaut", "Taichiut", "Dorbet", "Manghud", "Ongirat", "Ikires", "Baya'ud", "Uru'ud", "Noyad",
                "Tayichi'ud"
            ]
        )
    };

    private static readonly Pool FallbackPool = new(
        [
            "Aldric", "Brenna", "Caelan", "Dorin", "Elowen", "Faren", "Gorin", "Halia", "Idris", "Jorah", "Kael",
            "Lyra", "Maren", "Niran", "Orin", "Pira", "Quill", "Rael", "Soren", "Talia"
        ],
        [
            "Ashvale", "Blackmere", "Coldwyn", "Duskmore", "Elmvale", "Frostholm", "Greymoor", "Harwick", "Ironfeld",
            "Jaedwyn", "Keldmere", "Larkmoor", "Mistfall", "Northvale", "Oakfen", "Pinegard", "Quarrymere", "Ravenmoor",
            "Stonegard", "Thornmere"
        ]
    );

    public static string GetName(string cultureStyle) {
        var pool = Pools.GetValueOrDefault(cultureStyle, FallbackPool);
        var first = pool.FirstNames[Random.Shared.Next(pool.FirstNames.Length)];
        var last = pool.LastNames[Random.Shared.Next(pool.LastNames.Length)];
        return $"{first} {last}";
    }

    public static (string[] FirstNames, string[] LastNames) GetPool(string cultureStyle) {
        var pool = Pools.GetValueOrDefault(cultureStyle, FallbackPool);
        return (pool.FirstNames, pool.LastNames);
    }

    private record Pool(string[] FirstNames, string[] LastNames);
}