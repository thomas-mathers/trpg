using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Algorithms;
using TRPG.Models;

namespace TRPG.Generators;

internal class WorldGeneratorInput {
    public required string Description { get; init; }
    public int FactionCount { get; init; }
    public int HousesPerCity { get; init; }
    public int MaxDungeons { get; init; }
    public int MaxFactionMembers { get; init; }
    public int MinDungeons { get; init; }
    public int MinFactionMembers { get; init; }
    public int MaxRuralRegions { get; init; }
    public int MaxUrbanRegions { get; init; }
    public int MaxCountries { get; init; }
    public int MinRuralRegions { get; init; }
    public int MinUrbanRegions { get; init; }
    public int MinCountries { get; init; }
    public int RaceCount { get; init; }
}

internal class WorldGeneratorResult {
    public required IReadOnlyCollection<PersonAbility> Abilities { get; init; }
    public required IReadOnlyList<BuildingOwner> BuildingOwners { get; init; }
    public required IReadOnlyList<Building> Buildings { get; init; }
    public required IReadOnlyList<Region> Regions { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<FactionMember> FactionMembers { get; init; }
    public required IReadOnlyList<Faction> Factions { get; init; }
    public required IReadOnlyList<InventoryItem> InventoryItems { get; init; }
    public required IReadOnlyList<Item> Items { get; init; }
    public required IReadOnlyList<Person> Persons { get; init; }
    public required IReadOnlyList<Prop> Props { get; init; }
    public required IReadOnlyList<Race> Races { get; init; }
    public required IReadOnlyList<Road> Roads { get; init; }
    public required IReadOnlyList<Room> Rooms { get; init; }
    public required IReadOnlyCollection<PersonSkill> Skills { get; init; }
    public required World World { get; init; }
}

internal class WorldGenerator(
    BuildingGenerator buildingGenerator,
    FactionsGenerator factionsGenerator,
    GeographyGenerator geographyGenerator,
    PersonGenerator personGenerator,
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
                Count = generatorInput.RaceCount
            },
            cancellationToken
        );

        var factions = await factionsGenerator.Generate(
            new FactionsGeneratorInput {
                WorldId = worldId,
                Description = generatorInput.Description,
                Count = generatorInput.FactionCount
            },
            cancellationToken
        );

        var geography = await geographyGenerator.Generate(
            new GeographyGeneratorInput {
                WorldId = worldId,
                Description = generatorInput.Description,
                Races = races,
                MaxRuralRegions = generatorInput.MaxRuralRegions,
                MaxUrbanRegions = generatorInput.MaxUrbanRegions,
                MaxCountries = generatorInput.MaxCountries,
                MinRuralRegions = generatorInput.MinRuralRegions,
                MinUrbanRegions = generatorInput.MinUrbanRegions,
                MinCountries = generatorInput.MinCountries
            },
            cancellationToken
        );

        var buildings = new List<Building>();
        var persons = new List<Person>();
        var buildingOwners = new List<BuildingOwner>();
        var factionMembers = new List<FactionMember>();
        var items = new List<Item>();
        var inventoryItems = new List<InventoryItem>();
        var rooms = new List<Room>();
        var props = new List<Prop>();
        var skills = new List<PersonSkill>();
        var abilities = new List<PersonAbility>();
        var guildHallIndex = 0;

        var ruralRegions = geography.Regions.Where(r => r.RegionType == RegionType.Rural).ToList();
        var dungeonCount = Math.Min(
            Random.Shared.Next(generatorInput.MinDungeons, generatorInput.MaxDungeons + 1),
            ruralRegions.Count);
        var dungeonRegions = ruralRegions.Shuffle().Take(dungeonCount).ToHashSet();

        foreach (var region in geography.Regions.Where(r => r.RegionType == RegionType.Urban)) {
            var cityBuildings = new List<Building>();

            foreach (var type in GetCityBuildingTypes(generatorInput.HousesPerCity)) {
                var race = races[Random.Shared.Next(races.Count)];
                var location = new Location { RegionId = region.Id, Coordinates = new Point(0, 0) };
                var personResult = personGenerator.Generate(
                    new PersonGeneratorInput(race, GetProfessionForBuilding(type), worldId, region.Id, location)
                );

                var memberIds = new List<Guid> { personResult.Person.Id };
                var memberPersons = new List<PersonGeneratorResult>();

                if (type == BuildingType.GuildHall) {
                    var numMembers = Random.Shared.Next(generatorInput.MinFactionMembers, generatorInput.MaxFactionMembers + 1);
                    for (var m = 1; m < numMembers; m++) {
                        var memberRace = races[Random.Shared.Next(races.Count)];
                        var memberLocation = new Location { RegionId = region.Id, Coordinates = new Point(0, 0) };
                        var memberPerson = personGenerator.Generate(
                            new PersonGeneratorInput(memberRace, Profession.Mercenary, worldId, region.Id, memberLocation)
                        );
                        memberPersons.Add(memberPerson);
                        memberIds.Add(memberPerson.Person.Id);
                    }
                }

                var buildingResult = buildingGenerator.Generate(
                    new BuildingGeneratorInput(RegionId: region.Id, RegionType: region.RegionType,
                        OwnerId: personResult.Person.Id, Type: type) { MemberIds = memberIds }
                );

                if (type == BuildingType.GuildHall) {
                    var factionId = factions[guildHallIndex++ % factions.Count].Id;
                    buildingResult.Building.FactionId = factionId;
                    factionMembers.Add(new FactionMember
                        { FactionId = factionId, PersonId = personResult.Person.Id, Role = FactionRole.Leader });
                    foreach (var memberPerson in memberPersons) {
                        factionMembers.Add(new FactionMember
                            { FactionId = factionId, PersonId = memberPerson.Person.Id, Role = FactionRole.Member });
                        memberPerson.Person.Location.BuildingId = buildingResult.Building.Id;
                        persons.Add(memberPerson.Person);
                        items.AddRange(memberPerson.Items);
                        inventoryItems.AddRange(memberPerson.InventoryItems);
                        skills.AddRange(memberPerson.Skills);
                        abilities.AddRange(memberPerson.Abilities);
                    }
                }

                personResult.Person.Location.BuildingId = buildingResult.Building.Id;
                cityBuildings.Add(buildingResult.Building);
                persons.Add(personResult.Person);
                items.AddRange(personResult.Items);
                inventoryItems.AddRange(personResult.InventoryItems);
                skills.AddRange(personResult.Skills);
                abilities.AddRange(personResult.Abilities);
                buildingOwners.Add(new BuildingOwner
                    { BuildingId = buildingResult.Building.Id, OwnerId = personResult.Person.Id });
                rooms.AddRange(buildingResult.Rooms);
                props.AddRange(buildingResult.Props);
            }

            BuildingLayout.PlaceBuildings(region, cityBuildings);
            buildings.AddRange(cityBuildings);
        }

        foreach (var region in dungeonRegions) {
            var result = DungeonGenerator.Generate(new DungeonGeneratorInput(region.Id));
            BuildingLayout.PlaceBuildings(region, [result.Building]);
            buildings.Add(result.Building);
            rooms.Add(result.Room);
        }

        logger.LogDebug("GenerateWorld completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new WorldGeneratorResult {
            World = geography.World,
            Countries = geography.Countries,
            Regions = geography.Regions,
            Roads = geography.Roads,
            Races = races,
            Factions = factions,
            Buildings = buildings,
            Persons = persons,
            BuildingOwners = buildingOwners,
            FactionMembers = factionMembers,
            Items = items,
            InventoryItems = inventoryItems,
            Rooms = rooms,
            Props = props,
            Skills = skills,
            Abilities = abilities
        };
    }

    private static List<BuildingType> GetCityBuildingTypes(int housesPerCity) {
        var types = StandardBuildingTypes.ToList();
        for (var i = 0; i < housesPerCity; i++) {
            types.Add(BuildingType.House);
        }

        return types;
    }

    private static Profession GetProfessionForBuilding(BuildingType type) {
        return type switch {
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
}