using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Algorithms;
using TRPG.Models;

namespace TRPG.Commands;

internal class GenerateWorldCommand {
    public required string Description { get; init; }
    public int FactionCount { get; init; }
    public int HousesPerCity { get; init; }
    public int MaxCities { get; init; }
    public int MaxCountries { get; init; }
    public int MinCities { get; init; }
    public int MinCountries { get; init; }
    public int RaceCount { get; init; }
}

internal class GenerateWorldCommandResult {
    public required IReadOnlyList<BuildingOwner> BuildingOwners { get; init; }
    public required IReadOnlyList<Building> Buildings { get; init; }
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<Faction> Factions { get; init; }
    public required IReadOnlyList<Person> Persons { get; init; }
    public required IReadOnlyList<Prop> Props { get; init; }
    public required IReadOnlyList<Race> Races { get; init; }
    public required IReadOnlyList<Road> Roads { get; init; }
    public required IReadOnlyList<Room> Rooms { get; init; }
    public required World World { get; init; }
}

internal class GenerateWorldCommandHandler(
    GenerateGeographyCommandHandler geographyHandler,
    GenerateRacesCommandHandler racesHandler,
    GenerateFactionsCommandHandler factionsHandler,
    ILogger<GenerateWorldCommandHandler> logger
) {
    private static readonly BuildingType[] StandardBuildingTypes = [
        BuildingType.ArcaneShop,
        BuildingType.Apothecary,
        BuildingType.Bakery,
        BuildingType.Blacksmith,
        BuildingType.GeneralGoods,
        BuildingType.Library,
        BuildingType.Stable,
        BuildingType.Tavern,
        BuildingType.Temple
    ];

    private static readonly BuildingType[] CapitalOnlyBuildingTypes = [
        BuildingType.Castle,
        BuildingType.Jail
    ];

    private static readonly Dictionary<BuildingType, string[]> BuildingNames = new() {
        [BuildingType.ArcaneShop] = [
            "The Mystic Tome", "The Wandering Eye", "The Silver Sigil", "The Hidden Grimoire",
            "The Arcane Emporium", "Enchanted Relics", "The Spellwright's Corner",
            "The Runic Cache", "The Veil & Vellum", "The Curious Curio"
        ],
        [BuildingType.Apothecary] = [
            "The Healing Hand", "The Green Flask", "The Herb Garden", "The Mortar & Pestle",
            "The Remedy Shop", "The Alchemist's Corner", "The Tincture & Tonic",
            "The Potion Cellar", "Yarrow & Rue", "The Physick Hall"
        ],
        [BuildingType.Bakery] = [
            "The Golden Loaf", "The Warm Hearth", "The Rising Crust", "The Kneaded Dough",
            "The Crumb & Crust", "The Ember Oven", "The Harvest Loaf",
            "The Sweet Roll", "The Grain Basket", "The Flour Mill"
        ],
        [BuildingType.Blacksmith] = [
            "The Iron Anvil", "The Rusty Hammer", "The Forge & Fire", "The Steel Works",
            "The Hammered Shield", "The Ember Forge", "The Ironmonger",
            "The Tempered Blade", "The Smoldering Coal", "The Striker's Anvil"
        ],
        [BuildingType.Castle] = [
            "The Iron Keep", "The Stone Fortress", "The High Bastion", "The Warden's Tower",
            "The Royal Seat", "The Grey Citadel", "The Rampart Keep",
            "The Duke's Stronghold", "The Iron Seat", "The Sunken Keep"
        ],
        [BuildingType.GeneralGoods] = [
            "The Trading Post", "The Market Stall", "The Supply Corner", "The Provisions Shop",
            "The Sundry Store", "The Peddler's Pack", "The Dry Goods",
            "The Common Wares", "The Merchant's Cache", "The Open Barrel"
        ],
        [BuildingType.GuildHall] = [
            "The Guild House", "The Order Hall", "The Brotherhood Hall", "The Common Charter",
            "The Sealed Compact", "The Meeting Lodge", "The Trade Moot",
            "The Charter House", "The Open Hall", "The Register Room"
        ],
        [BuildingType.Jail] = [
            "The Iron Gate", "The Stone Cell", "The Warden's Hold", "The Grim Cage",
            "The Cold Bar", "The Mute Dungeon", "The Debtor's Hold",
            "The Lock & Chain", "The Sober Room", "The Quiet Pit"
        ],
        [BuildingType.Library] = [
            "The Dusty Pages", "The Scholar's Retreat", "The Open Book", "The Chronicle Hall",
            "The Lending Stack", "The Ink & Parchment", "The Bound Collection",
            "The Reading Nook", "The Archive Hall", "The Scrivener's Den"
        ],
        [BuildingType.Stable] = [
            "The Wanderer's Rest", "The Iron Shoe", "The Haystack", "The Rider's Lodge",
            "The Trodden Path", "The Oat & Saddle", "The Hitching Post",
            "The Canter & Comb", "The Straw Bale", "The Horseman's Haven"
        ],
        [BuildingType.Tavern] = [
            "The Rusty Flagon", "The Wandering Bard", "The Hearth & Hound", "The Tipped Barrel",
            "The Crooked Stool", "The Salted Rim", "The Ember & Ale",
            "The Common Room", "The Stumbling Pilgrim", "The Last Drop"
        ],
        [BuildingType.Temple] = [
            "The Sacred Flame", "The Divine Light", "The Pilgrim's Shrine", "The Holy Hearth",
            "The Sanctum of Faith", "The Blessed Hall", "The Quiet Chapel",
            "The Offering Stone", "The Votive Lantern", "The Celestial Gate"
        ]
    };

    public async Task<GenerateWorldCommandResult> Handle(
        GenerateWorldCommand command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();

        var worldId = Guid.NewGuid();

        var races = await racesHandler.Handle(
            new GenerateRacesCommand
                { Count = command.RaceCount, Description = command.Description, WorldId = worldId },
            cancellationToken
        );

        var geography = await geographyHandler.Handle(
            new GenerateGeographyCommand {
                Description = command.Description,
                MaxCities = command.MaxCities,
                MaxCountries = command.MaxCountries,
                MinCities = command.MinCities,
                MinCountries = command.MinCountries,
                Races = races,
                WorldId = worldId
            },
            cancellationToken
        );
        var factions = await factionsHandler.Handle(
            new GenerateFactionsCommand
                { Count = command.FactionCount, Description = command.Description, WorldId = worldId },
            cancellationToken
        );

        var buildingsByCity = geography.Cities.ToDictionary(c => c.Id, _ => new List<Building>());

        foreach (var city in geography.Cities) {
            var types = city.IsCapital
                ? [..StandardBuildingTypes, ..CapitalOnlyBuildingTypes]
                : StandardBuildingTypes;

            buildingsByCity[city.Id].AddRange(types.Select(type => new Building {
                CityId = city.Id,
                BuildingType = type,
                Name = BuildingNames.TryGetValue(type, out var names)
                    ? names[Random.Shared.Next(names.Length)]
                    : type.ToString(),
                Boundary = new Rectangle(0, 0, 0, 0)
            }));

            for (var i = 1; i <= command.HousesPerCity; i++) {
                buildingsByCity[city.Id].Add(new Building {
                    CityId = city.Id,
                    BuildingType = BuildingType.House,
                    Name = $"House {i}",
                    Description = "A modest residential dwelling.",
                    Boundary = new Rectangle(0, 0, 0, 0)
                });
            }
        }

        var allBuildings = buildingsByCity.Values.SelectMany(b => b).ToList();

        var guildHalls = allBuildings.Where(b => b.BuildingType == BuildingType.GuildHall).ToList();

        foreach (var faction in factions) {
            if (guildHalls.Count == 0) {
                var city = geography.Cities[Random.Shared.Next(geography.Cities.Count)];
                var hall = new Building {
                    CityId = city.Id,
                    BuildingType = BuildingType.GuildHall,
                    Name = $"{faction.Name} Hall",
                    Description = $"The guild hall of {faction.Name}.",
                    Boundary = new Rectangle(0, 0, 0, 0),
                    FactionId = faction.Id
                };
                buildingsByCity[city.Id].Add(hall);
                allBuildings.Add(hall);
            }
            else {
                var hall = guildHalls[0];
                guildHalls.RemoveAt(0);
                hall.FactionId = faction.Id;
            }
        }

        var factionIndex = 0;
        foreach (var hall in guildHalls) {
            hall.FactionId = factions[factionIndex++ % factions.Count].Id;
        }

        foreach (var city in geography.Cities) {
            CityLayout.PlaceBuildings(city, buildingsByCity[city.Id]);
        }

        var owners = new List<Person>();
        var buildingOwners = new List<BuildingOwner>();
        var allProfessions = Enum.GetValues<Profession>();

        foreach (var city in geography.Cities) {
            foreach (var building in buildingsByCity[city.Id]) {
                var race = races[Random.Shared.Next(races.Count)];
                var profession = allProfessions[Random.Shared.Next(allProfessions.Length)];
                var name = NameDefinitions.GetName(race.CultureStyle);

                var owner = new Person {
                    WorldId = worldId,
                    Name = name,
                    RaceId = race.Id,
                    Profession = profession,
                    BirthCityId = city.Id,
                    BirthYear = Random.Shared.Next(900, 975),
                    Gold = Random.Shared.Next(50, 500),
                    Location = new Location {
                        CityId = city.Id,
                        BuildingId = building.Id,
                        Coordinates = new Point(0, 0)
                    },
                    Attributes = new Attributes {
                        Hp = new Meter(100, 100),
                        Ap = new Meter(10, 10),
                        Strength = 5,
                        Defense = 5,
                        Dexterity = 5,
                        Endurance = 5,
                        Intelligence = 5
                    },
                    Level = 1
                };
                owners.Add(owner);
                buildingOwners.Add(new BuildingOwner { BuildingId = building.Id, OwnerId = owner.Id });
            }
        }

        var ownerById = buildingOwners.ToDictionary(bo => bo.BuildingId, bo => bo.OwnerId);
        var allRooms = new List<Room>();
        var allProps = new List<Prop>();
        foreach (var building in allBuildings) {
            var ownerId = ownerById.GetValueOrDefault(building.Id);
            var (rooms, props) = BuildingTemplate.Create(building.BuildingType, building.Id, ownerId);
            allRooms.AddRange(rooms);
            allProps.AddRange(props);
        }

        logger.LogDebug("GenerateWorld completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new GenerateWorldCommandResult {
            World = geography.World,
            Countries = geography.Countries,
            Cities = geography.Cities,
            Roads = geography.Roads,
            Races = races,
            Factions = factions,
            Buildings = allBuildings,
            Persons = owners,
            BuildingOwners = buildingOwners,
            Rooms = allRooms,
            Props = allProps
        };
    }
}