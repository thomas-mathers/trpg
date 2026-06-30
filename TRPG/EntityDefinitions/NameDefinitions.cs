namespace TRPG.EntityDefinitions;

internal static class NameDefinitions {
    private static readonly Dictionary<string, Pool> Pools = new(StringComparer.OrdinalIgnoreCase) {
        ["Nordic/Viking"] = new Pool(
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
        ["Roman"] = new Pool(
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
        ["Feudal Japanese"] = new Pool(
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
        ["Arabic/Persian"] = new Pool(
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
        ["Celtic"] = new Pool(
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
        ["Slavic"] = new Pool(
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
        ["Ancient Egyptian"] = new Pool(
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
        ["Mesoamerican"] = new Pool(
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
        ["Byzantine"] = new Pool(
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
        ["Mongol"] = new Pool(
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

    private static readonly Pool FallbackPool = new(
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