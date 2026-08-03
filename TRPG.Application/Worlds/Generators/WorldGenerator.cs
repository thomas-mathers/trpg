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
    public required IReadOnlyCollection<CreatureAbility> Abilities { get; init; }
    public required IReadOnlyList<BuildingOwner> BuildingOwners { get; init; }
    public required IReadOnlyList<Building> Buildings { get; init; }
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<Creature> Creatures { get; init; }
    public required IReadOnlyList<District> Districts { get; init; }
    public required IReadOnlyList<FactionMember> FactionMembers { get; init; }
    public required IReadOnlyList<Faction> Factions { get; init; }
    public required IReadOnlyList<Item> Items { get; init; }
    public required IReadOnlyList<CreatureJob> Jobs { get; init; }
    public required IReadOnlyList<CreatureKnowledge> Knowledge { get; init; }
    public required IReadOnlyList<Location> Locations { get; init; }
    public required IReadOnlyList<Prop> Props { get; init; }
    public required IReadOnlyList<Relationship> Relationships { get; init; }
    public required IReadOnlyList<Road> Roads { get; init; }
    public required IReadOnlyList<RoomConnectorKey> RoomConnectorKeys { get; init; }
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
        var buildings = new List<Building>();
        var creatures = new List<Creature>();
        var buildingOwners = new List<BuildingOwner>();
        var factionMembers = new List<FactionMember>();
        var items = new List<Item>();
        var rooms = new List<Room>();
        var locations = new List<Location>(geography.Locations);
        var props = new List<Prop>();
        var skills = new List<CreatureSkill>();
        var abilities = new List<CreatureAbility>();
        var jobs = new List<CreatureJob>();
        var roomConnectorKeys = new List<RoomConnectorKey>();
        var relationships = new List<Relationship>();

        var stateById = geography.States.ToDictionary(s => s.Id);
        var districtsByCityId = geography
            .Districts.GroupBy(d => d.CityId)
            .ToDictionary(g => g.Key, g => g.ToList());

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
            skills.AddRange(cityResult.Skills);
            abilities.AddRange(cityResult.Abilities);
            jobs.AddRange(cityResult.Jobs);
            roomConnectorKeys.AddRange(cityResult.RoomConnectorKeys);
            relationships.AddRange(cityResult.Relationships);
        }

        var monsters = new List<Creature>();
        foreach (var state in geography.States)
        {
            var count = Random.Shared.Next(
                generatorInput.MinBuildingsPerState,
                generatorInput.MaxBuildingsPerState + 1
            );
            var usedNames = new HashSet<string>();
            for (var i = 0; i < count; i++)
            {
                var result = DungeonGenerator.Generate(
                    new DungeonGeneratorInput(state.Id, usedNames, worldId)
                );
                usedNames.Add(result.Building.Name);
                buildings.Add(result.Building);
                rooms.Add(result.Room);
                locations.Add(result.Location);
                props.AddRange(result.Props);

                var dungeonMonsters = dungeonPopulator.Generate(
                    new DungeonPopulatorInput
                    {
                        StateId = state.Id,
                        LocationId = result.Location.Id,
                        WorldId = worldId,
                        DungeonType = result.Building.BuildingType,
                    }
                );
                monsters.AddRange(dungeonMonsters.Select(m => m.Creature));
                items.AddRange(dungeonMonsters.SelectMany(m => m.Items));
                skills.AddRange(dungeonMonsters.SelectMany(m => m.Skills));
                abilities.AddRange(dungeonMonsters.SelectMany(m => m.Abilities));
            }
        }

        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                creatures,
                stateById,
                factionMembers,
                factions,
                relationships,
                jobs,
                rooms,
                buildings,
                buildingOwners
            )
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
                Locations = locations,
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
            Roads = geography.Roads,
            Factions = factions,
            Buildings = buildings,
            Creatures = creatures,
            BuildingOwners = buildingOwners,
            FactionMembers = factionMembers,
            Items = items,
            Rooms = rooms,
            Locations = locations,
            Props = props,
            Skills = skills,
            Abilities = abilities,
            Jobs = jobs,
            Knowledge = knowledge,
            RoomConnectorKeys = roomConnectorKeys,
            Relationships = relationships,
        };
    }
}
