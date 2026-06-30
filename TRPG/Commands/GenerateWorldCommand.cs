using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Algorithms;
using TRPG.EntityDefinitions;
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
    public required IReadOnlyList<InventoryItem> InventoryItems { get; init; }
    public required IReadOnlyList<Item> Items { get; init; }
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
    ItemDefinitions itemDefinitions,
    ILogger<GenerateWorldCommandHandler> logger
) {
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

        var buildingsByCity = CreateAndPlaceBuildings(geography, factions, command.HousesPerCity);
        var allBuildings = buildingsByCity.Values.SelectMany(b => b).ToList();
        var (owners, buildingOwners) = GenerateOwners(new GenerateOwnersInput(geography.Cities, buildingsByCity, races, worldId));
        var (allItems, allInventoryItems) = GenerateInventories(owners, itemDefinitions, worldId);
        var (allRooms, allProps) = GenerateRoomsAndProps(allBuildings, buildingOwners);

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
            Items = allItems,
            InventoryItems = allInventoryItems,
            Rooms = allRooms,
            Props = allProps
        };
    }

    private static Dictionary<Guid, List<Building>> CreateAndPlaceBuildings(
        GenerateGeographyCommandResult geography,
        IReadOnlyList<Faction> factions,
        int housesPerCity
    ) {
        var buildingsByCity = geography.Cities.ToDictionary(
            c => c.Id,
            c => BuildingTemplate.GenerateForCity(c, housesPerCity).ToList()
        );
        AssignFactionHalls(buildingsByCity, factions, geography.Cities);
        foreach (var city in geography.Cities) {
            CityLayout.PlaceBuildings(city, buildingsByCity[city.Id]);
        }
        return buildingsByCity;
    }

    private static GeneratedInventories GenerateInventories(
        IReadOnlyList<Person> owners,
        ItemDefinitions definitions,
        Guid worldId
    ) {
        var items = new List<Item>();
        var inventoryItems = new List<InventoryItem>();
        foreach (var owner in owners) {
            var (ownerItems, ownerInventoryItems) = definitions.GenerateStartingInventory(owner.Profession, owner.Id, worldId);
            items.AddRange(ownerItems);
            inventoryItems.AddRange(ownerInventoryItems);
        }
        return new GeneratedInventories(items, inventoryItems);
    }

    private static void AssignFactionHalls(
        Dictionary<Guid, List<Building>> buildingsByCity,
        IReadOnlyList<Faction> factions,
        IReadOnlyList<City> cities
    ) {
        var guildHalls = buildingsByCity.Values.SelectMany(b => b)
            .Where(b => b.BuildingType == BuildingType.GuildHall).ToList();

        foreach (var faction in factions) {
            if (guildHalls.Count == 0) {
                var city = cities[Random.Shared.Next(cities.Count)];
                var hall = new Building {
                    CityId = city.Id,
                    BuildingType = BuildingType.GuildHall,
                    Name = $"{faction.Name} Hall",
                    Description = $"The guild hall of {faction.Name}.",
                    Boundary = new Rectangle(0, 0, 0, 0),
                    FactionId = faction.Id
                };
                buildingsByCity[city.Id].Add(hall);
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
    }

    private static GeneratedOwners GenerateOwners(GenerateOwnersInput input) {
        var owners = new List<Person>();
        var buildingOwners = new List<BuildingOwner>();
        var allProfessions = Enum.GetValues<Profession>();

        foreach (var city in input.Cities) {
            foreach (var building in input.BuildingsByCity[city.Id]) {
                var race = input.Races[Random.Shared.Next(input.Races.Count)];
                var profession = allProfessions[Random.Shared.Next(allProfessions.Length)];
                var owner = new Person {
                    WorldId = input.WorldId,
                    Name = NameDefinitions.GetName(race.CultureStyle),
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

        return new GeneratedOwners(owners, buildingOwners);
    }

    private static GeneratedRoomsAndProps GenerateRoomsAndProps(
        IReadOnlyList<Building> buildings,
        List<BuildingOwner> buildingOwners
    ) {
        var ownerById = buildingOwners.ToDictionary(bo => bo.BuildingId, bo => bo.OwnerId);
        var rooms = new List<Room>();
        var props = new List<Prop>();

        foreach (var building in buildings) {
            var ownerId = ownerById.GetValueOrDefault(building.Id);
            var (buildingRooms, buildingProps) = BuildingTemplate.Create(building.BuildingType, building.Id, ownerId);
            rooms.AddRange(buildingRooms);
            props.AddRange(buildingProps);
        }

        return new GeneratedRoomsAndProps(rooms, props);
    }
}

internal record GeneratedOwners(List<Person> Owners, List<BuildingOwner> BuildingOwners);

internal record GenerateOwnersInput(
    IReadOnlyList<City> Cities,
    Dictionary<Guid, List<Building>> BuildingsByCity,
    IReadOnlyList<Race> Races,
    Guid WorldId
);

internal record GeneratedInventories(List<Item> Items, List<InventoryItem> InventoryItems);

internal record GeneratedRoomsAndProps(List<Room> Rooms, List<Prop> Props);