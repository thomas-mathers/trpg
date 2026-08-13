using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public class WorldGeneratorInput
{
    public required string Description { get; init; }
    public int FactionCount { get; init; }
    public int HousesPerCity { get; init; }
    public int MaxBuildingsPerState { get; init; }
    public int MaxCityStates { get; init; }
    public int MaxFactionMembers { get; init; }
    public int MaxHouseholdSize { get; init; }
    public int MaxRuralStates { get; init; }
    public int MinBuildingsPerState { get; init; }
    public int MinCityStates { get; init; }
    public int MinFactionMembers { get; init; }
    public int MinHouseholdSize { get; init; }
    public int MinRuralStates { get; init; }
}

public class WorldGeneratorResult
{
    public required IReadOnlyList<BuildingOwner> BuildingOwners { get; init; }
    public required IReadOnlyList<Building> Buildings { get; init; }
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<Creature> Creatures { get; init; }
    public required IReadOnlyList<District> Districts { get; init; }
    public required IReadOnlyList<EncounterGroup> EncounterGroups { get; init; }
    public required IReadOnlyList<EncounterGroupMember> EncounterGroupMembers { get; init; }
    public required IReadOnlyList<FactionMember> FactionMembers { get; init; }
    public required IReadOnlyList<Faction> Factions { get; init; }
    public required IReadOnlyList<Item> Items { get; init; }
    public required IReadOnlyList<CreatureJob> Jobs { get; init; }
    public required IReadOnlyList<CreatureKnowledge> Knowledge { get; init; }
    public required IReadOnlyList<Location> Locations { get; init; }
    public required IReadOnlyList<Prop> Props { get; init; }
    public required IReadOnlyList<Relationship> Relationships { get; init; }
    public required IReadOnlyList<LocationConnector> LocationConnectors { get; init; }
    public required IReadOnlyList<DoorConnector> DoorConnectors { get; init; }
    public required IReadOnlyList<TravelConnector> TravelConnectors { get; init; }
    public required IReadOnlyList<DoorConnectorKey> DoorConnectorKeys { get; init; }
    public required IReadOnlyList<Room> Rooms { get; init; }
    public required IReadOnlyCollection<CreatureSkill> Skills { get; init; }
    public required IReadOnlyList<State> States { get; init; }
    public required World World { get; init; }
}

public class WorldGenerator(
    FactionsGenerator factionsGenerator,
    GeographyGenerator geographyGenerator,
    CityGenerator cityGenerator,
    DungeonPopulator dungeonPopulator,
    ILogger<WorldGenerator> logger
)
{
    public async Task<WorldGeneratorResult> Generate(
        WorldGeneratorInput generatorInput,
        CancellationToken cancellationToken
    )
    {
        if (generatorInput.HousesPerCity > BuildingGenerator.Names[BuildingType.House].Length)
        {
            throw new InvalidOperationException(
                $"HousesPerCity ({generatorInput.HousesPerCity}) cannot exceed the house name pool size ({BuildingGenerator.Names[BuildingType.House].Length})."
            );
        }

        if (generatorInput.MaxBuildingsPerState > DungeonGenerator.TotalNameCount)
        {
            throw new InvalidOperationException(
                $"MaxBuildingsPerState ({generatorInput.MaxBuildingsPerState}) cannot exceed the dungeon name pool size ({DungeonGenerator.TotalNameCount})."
            );
        }

        var sw = Stopwatch.StartNew();
        var worldId = Guid.NewGuid();

        var groundedDescription = $"""
            {generatorInput.Description} This remains a low-fantasy world where knights,
            mercenaries, blacksmiths, and mages are established, respected roles, and swords
            and plate armor are still standard equipment. Any technological or aesthetic
            theme should layer on top of this as mood and atmosphere — not replace or
            obsolete these roles and equipment. The peoples of this world are Humans, Elves,
            Dwarves, Orcs, Halflings, and Gnomes — do not invent other playable races (no
            Tieflings, Dragonborn, Elementals, or similar) though monstrous threats like
            undead, demons, and beasts may lurk at the margins of civilization.
            """;

        var namedFactions = (
            await factionsGenerator.Generate(
                new FactionsGeneratorInput
                {
                    WorldId = worldId,
                    Description = groundedDescription,
                    Count = generatorInput.FactionCount,
                },
                cancellationToken
            )
        ).ToList();

        var geography = await geographyGenerator.Generate(
            new GeographyGeneratorInput
            {
                WorldId = worldId,
                Description = groundedDescription,
                MaxRuralStates = generatorInput.MaxRuralStates,
                MaxCityStates = generatorInput.MaxCityStates,
                MinRuralStates = generatorInput.MinRuralStates,
                MinCityStates = generatorInput.MinCityStates,
            },
            cancellationToken
        );

        var factions = new List<Faction>(namedFactions);
        var encounterFactionsByCreatureType = EncounterFactionGenerator.Generate(worldId);
        factions.AddRange(encounterFactionsByCreatureType.Values);
        var buildings = new List<Building>();
        var creatures = new List<Creature>();
        var buildingOwners = new List<BuildingOwner>();
        var factionMembers = new List<FactionMember>();
        var items = new List<Item>();
        var rooms = new List<Room>();
        var locations = new List<Location>(geography.Locations);
        var props = new List<Prop>(geography.Props);
        var locationConnectors = new List<LocationConnector>(geography.LocationConnectors);
        var doorConnectors = new List<DoorConnector>();
        var travelConnectors = new List<TravelConnector>();
        var skills = new List<CreatureSkill>();
        var jobs = new List<CreatureJob>();
        var doorConnectorKeys = new List<DoorConnectorKey>();
        var relationships = new List<Relationship>();
        var encounterGroups = new List<EncounterGroup>();
        var encounterGroupMembers = new List<EncounterGroupMember>();

        var stateById = geography.States.ToDictionary(s => s.Id);
        var districtsByCityId = geography
            .Districts.GroupBy(d => d.CityId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var citiesByStateId = geography
            .Cities.GroupBy(c => c.StateId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var locationsById = geography.Locations.ToDictionary(location => location.Id);

        foreach (var city in geography.Cities)
        {
            var cityResult = cityGenerator.Generate(
                new CityGeneratorInput
                {
                    WorldId = worldId,
                    City = city,
                    State = stateById[city.StateId],
                    DominantRace = geography.DominantRaceByCountryId[city.CountryId],
                    Districts = districtsByCityId[city.Id],
                    LocationsById = locationsById,
                    NamedFactions = namedFactions,
                    GeneratorInput = generatorInput,
                }
            );

            factions.Add(cityResult.CityFaction);
            buildings.AddRange(cityResult.Buildings);
            creatures.AddRange(cityResult.Creatures);
            buildingOwners.AddRange(cityResult.BuildingOwners);
            factionMembers.AddRange(cityResult.FactionMembers);
            items.AddRange(cityResult.Items);
            rooms.AddRange(cityResult.Rooms);
            locations.AddRange(cityResult.Locations);
            props.AddRange(cityResult.Props);
            locationConnectors.AddRange(cityResult.LocationConnectors);
            doorConnectors.AddRange(cityResult.DoorConnectors);
            skills.AddRange(cityResult.Skills);
            jobs.AddRange(cityResult.Jobs);
            doorConnectorKeys.AddRange(cityResult.DoorConnectorKeys);
            relationships.AddRange(cityResult.Relationships);
        }

        var monsters = new List<Creature>();
        var wildernessLocationByStateId = new Dictionary<Guid, Location>();
        foreach (var state in geography.States)
        {
            var count = Random.Shared.Next(
                generatorInput.MinBuildingsPerState,
                generatorInput.MaxBuildingsPerState + 1
            );
            var wildernessLocation = LocationGenerator.Generate(worldId, state.Id);
            locations.Add(wildernessLocation);
            wildernessLocationByStateId[state.Id] = wildernessLocation;

            if (citiesByStateId.TryGetValue(state.Id, out var citiesInState))
            {
                foreach (var city in citiesInState)
                {
                    var cityEntranceDistrict = districtsByCityId[city.Id]
                        .First(d => d.DistrictType == DistrictType.CityEntrance);
                    var connectorResult = WildernessConnectorGenerator.Generate(
                        city,
                        cityEntranceDistrict,
                        wildernessLocation,
                        worldId
                    );
                    locationConnectors.AddRange(connectorResult.LocationConnectors);
                    travelConnectors.AddRange(connectorResult.TravelConnectors);
                }
            }

            if (count == 0)
            {
                continue;
            }

            var usedNames = new HashSet<string>();
            for (var i = 0; i < count; i++)
            {
                var result = DungeonGenerator.Generate(
                    new DungeonGeneratorInput(usedNames, wildernessLocation, worldId)
                );
                usedNames.Add(result.Building.Name);
                buildings.Add(result.Building);
                rooms.Add(result.Room);
                locations.Add(result.Location);
                locationConnectors.Add(result.FrontDoor);
                doorConnectors.Add(result.Door);

                var dungeonMonsters = dungeonPopulator.Generate(
                    new DungeonPopulatorInput
                    {
                        LocationId = result.Location.Id,
                        WorldId = worldId,
                        DungeonType = result.Building.BuildingType,
                        FactionsByCreatureType = encounterFactionsByCreatureType,
                    }
                );
                monsters.AddRange(dungeonMonsters.Monsters.Select(monster => monster.Creature));
                items.AddRange(dungeonMonsters.Monsters.SelectMany(monster => monster.Items));
                skills.AddRange(dungeonMonsters.Monsters.SelectMany(monster => monster.Skills));
                encounterGroups.AddRange(dungeonMonsters.EncounterGroups);
                encounterGroupMembers.AddRange(dungeonMonsters.EncounterGroupMembers);
            }
        }

        foreach (var link in geography.StateTravelLinks)
        {
            AddTravelConnector(link.OriginStateId, link.DestinationStateId, link);
            AddTravelConnector(link.DestinationStateId, link.OriginStateId, link);
        }

        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                creatures,
                locations.ToDictionary(location => location.Id),
                factionMembers,
                factions,
                relationships,
                jobs,
                rooms,
                buildings,
                buildingOwners
            )
        );

        var namedLocations = LocationNameGenerator.Generate(
            locations,
            geography.States,
            geography.Cities,
            geography.Districts,
            buildings,
            rooms
        );

        var knowledge = KnowledgeGenerator.Generate(
            new KnowledgeGeneratorInput
            {
                WorldId = worldId,
                Creatures = creatures,
                Relationships = relationships,
                FactionMembers = factionMembers,
                Factions = factions,
                Cities = geography.Cities,
                Locations = namedLocations,
                States = geography.States,
                Countries = geography.Countries,
            }
        );

        creatures.AddRange(monsters);

        logger.LogDebug("GenerateWorld completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new WorldGeneratorResult
        {
            World = geography.World,
            Countries = geography.Countries,
            States = geography.States,
            Cities = geography.Cities,
            Districts = geography.Districts,
            EncounterGroups = encounterGroups,
            EncounterGroupMembers = encounterGroupMembers,
            LocationConnectors = locationConnectors,
            DoorConnectors = doorConnectors,
            TravelConnectors = travelConnectors,
            Factions = factions,
            Buildings = buildings,
            Creatures = creatures,
            BuildingOwners = buildingOwners,
            FactionMembers = factionMembers,
            Items = items,
            Rooms = rooms,
            Locations = namedLocations,
            Props = props,
            Skills = skills,
            Jobs = jobs,
            Knowledge = knowledge,
            DoorConnectorKeys = doorConnectorKeys,
            Relationships = relationships,
        };

        void AddTravelConnector(Guid originStateId, Guid destinationStateId, StateTravelLink link)
        {
            var destinationState = stateById[destinationStateId];
            var connector = new LocationConnector
            {
                OriginLocationId = wildernessLocationByStateId[originStateId].Id,
                DestinationLocationId = wildernessLocationByStateId[destinationStateId].Id,
                Name = link.Name,
                Description = $"{link.Name} leads into {destinationState.Name}.",
                DestinationLabel = destinationState.Name,
                WorldId = worldId,
            };
            locationConnectors.Add(connector);
            travelConnectors.Add(
                new TravelConnector
                {
                    ConnectorId = connector.Id,
                    Distance = link.Distance,
                    DangerLevel = link.DangerLevel,
                    TravelTimeHours = link.TravelTimeHours,
                    WorldId = worldId,
                }
            );
        }
    }
}
