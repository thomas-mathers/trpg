using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Algorithms;
using TRPG.Models;

namespace TRPG.Generators;

internal class WorldGeneratorInput {
    public required string Description { get; init; }
    public int FactionCount { get; init; }
    public int HousesPerCity { get; init; }
    public int MaxCities { get; init; }
    public int MaxCountries { get; init; }
    public int MinCities { get; init; }
    public int MinCountries { get; init; }
    public int RaceCount { get; init; }
}

internal class WorldGeneratorResult {
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

internal class WorldGenerator(
    BuildingGenerator buildingGenerator,
    FactionsGenerator factionsGenerator,
    GeographyGenerator geographyGenerator,
    NpcGenerator npcGenerator,
    RaceGenerator raceGenerator,
    ILogger<WorldGenerator> logger
) {
    private static readonly BuildingType[] StandardBuildingTypes = [
        BuildingType.ArcaneShop, BuildingType.Apothecary, BuildingType.Bakery,
        BuildingType.Blacksmith, BuildingType.Castle, BuildingType.GeneralGoods,
        BuildingType.GuildHall, BuildingType.Jail, BuildingType.Library,
        BuildingType.Stable, BuildingType.Tavern, BuildingType.Temple
    ];

    public async Task<WorldGeneratorResult> Generate(
        WorldGeneratorInput generatorInput,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();
        var worldId = Guid.NewGuid();

        var races = await raceGenerator.Generate(
            new RaceGeneratorInput {
                WorldId = worldId,
                Description = generatorInput.Description,
                Count = generatorInput.RaceCount,
            },
            cancellationToken
        );

        var factions = await factionsGenerator.Generate(
            new FactionsGeneratorInput {
                WorldId = worldId,
                Description = generatorInput.Description,
                Count = generatorInput.FactionCount,
            },
            cancellationToken
        );

        var geography = await geographyGenerator.Generate(
            new GeographyGeneratorInput {
                WorldId = worldId,
                Description = generatorInput.Description,
                Races = races,
                MaxCities = generatorInput.MaxCities,
                MaxCountries = generatorInput.MaxCountries,
                MinCities = generatorInput.MinCities,
                MinCountries = generatorInput.MinCountries,
            },
            cancellationToken
        );

        var allBuildings = new List<Building>();
        var persons = new List<Person>();
        var buildingOwners = new List<BuildingOwner>();
        var items = new List<Item>();
        var inventoryItems = new List<InventoryItem>();
        var rooms = new List<Room>();
        var props = new List<Prop>();
        var guildHallIndex = 0;

        foreach (var city in geography.Cities) {
            var cityBuildings = new List<Building>();

            foreach (var type in GetCityBuildingTypes(generatorInput.HousesPerCity)) {
                var race = races[Random.Shared.Next(races.Count)];
                var location = new Location { CityId = city.Id, Coordinates = new Point(0, 0) };

                var npcResult = npcGenerator.Generate(
                    new NpcGeneratorInput(race, GetProfessionForBuilding(type), worldId, city.Id, location)
                );
                var buildingResult = buildingGenerator.Generate(
                    new BuildingGeneratorInput(city.Id, npcResult.Person.Id, type)
                );

                if (type == BuildingType.GuildHall) {
                    buildingResult.Building.FactionId = factions[guildHallIndex++ % factions.Count].Id;
                }

                npcResult.Person.Location.BuildingId = buildingResult.Building.Id;
                cityBuildings.Add(buildingResult.Building);
                persons.Add(npcResult.Person);
                items.AddRange(npcResult.Items);
                inventoryItems.AddRange(npcResult.InventoryItems);
                buildingOwners.Add(new BuildingOwner { BuildingId = buildingResult.Building.Id, OwnerId = npcResult.Person.Id });
                rooms.AddRange(buildingResult.Rooms);
                props.AddRange(buildingResult.Props);
            }

            CityLayout.PlaceBuildings(city, cityBuildings);
            allBuildings.AddRange(cityBuildings);
        }

        logger.LogDebug("GenerateWorld completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new WorldGeneratorResult {
            World = geography.World,
            Countries = geography.Countries,
            Cities = geography.Cities,
            Roads = geography.Roads,
            Races = races,
            Factions = factions,
            Buildings = allBuildings,
            Persons = persons,
            BuildingOwners = buildingOwners,
            Items = items,
            InventoryItems = inventoryItems,
            Rooms = rooms,
            Props = props
        };
    }

    private static List<BuildingType> GetCityBuildingTypes(int housesPerCity) {
        var types = StandardBuildingTypes.ToList();
        for (var i = 0; i < housesPerCity; i++) types.Add(BuildingType.House);
        return types;
    }

    private static Profession GetProfessionForBuilding(BuildingType type) => type switch {
        BuildingType.Tavern => Profession.Bartender,
        BuildingType.Blacksmith => Profession.Blacksmith,
        BuildingType.Temple => Profession.Cleric,
        BuildingType.Library => Profession.Scholar,
        BuildingType.GeneralGoods => Profession.Merchant,
        BuildingType.Apothecary => Profession.Alchemist,
        BuildingType.Bakery => Profession.Merchant,
        BuildingType.Stable => Profession.StableMaster,
        BuildingType.ArcaneShop => Profession.Mage,
        BuildingType.GuildHall => Profession.Politician,
        BuildingType.Castle => Profession.Politician,
        BuildingType.Jail => Profession.Guard,
        _ => Profession.Merchant
    };
}
