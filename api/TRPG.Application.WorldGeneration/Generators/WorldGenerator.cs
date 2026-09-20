using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

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
    public required IReadOnlyList<Quest> InitiationQuests { get; init; }
    public required IReadOnlyList<QuestObjective> InitiationQuestObjectives { get; init; }
    public required IReadOnlyList<FactionStanding> FactionStandings { get; init; }
    public IReadOnlyList<DungeonExpedition> DungeonExpeditions { get; init; } = [];
    public IReadOnlyList<BookWork> BookWorks { get; init; } = [];
    public IReadOnlyList<Fact> Facts { get; init; } = [];
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
    public required IReadOnlyList<CreatureProfile> CreatureProfiles { get; init; }
    public required IReadOnlyList<Prop> Props { get; init; }
    public required IReadOnlyList<Relationship> Relationships { get; init; }
    public required IReadOnlyList<LocationConnector> LocationConnectors { get; init; }
    public required IReadOnlyList<DoorConnector> DoorConnectors { get; init; }
    public required IReadOnlyList<TravelConnector> TravelConnectors { get; init; }
    public required IReadOnlyList<DoorConnectorKey> DoorConnectorKeys { get; init; }
    public required IReadOnlyList<DoorConnectorLever> DoorConnectorLevers { get; init; }
    public required IReadOnlyList<Room> Rooms { get; init; }
    public required IReadOnlyCollection<CreatureSkill> Skills { get; init; }
    public required IReadOnlyList<State> States { get; init; }
    public required World World { get; init; }
    public required IReadOnlyList<CreatureSpawner> CreatureSpawners { get; init; }
}

// Archetypes always leads with LeaderArchetype, which is also always Humanoid — an antagonist
// faction is led by a person, never sampled down to whatever monster archetype happened to win the
// ambient population roll.
internal record AntagonistLairSpec(
    string FactionName,
    BuildingType DungeonType,
    string LairName,
    IReadOnlyList<CreatureArchetype> Archetypes,
    CreatureArchetype LeaderArchetype
);

public class WorldGenerator(
    GeographyGenerator geographyGenerator,
    CityGenerator cityGenerator,
    DungeonPopulator dungeonPopulator,
    DungeonExpeditionGenerator dungeonExpeditionGenerator,
    DungeonInhabitantGenerator dungeonInhabitantGenerator,
    DungeonLootGenerator dungeonLootGenerator,
    DungeonTrapGenerator dungeonTrapGenerator,
    DungeonObstacleGenerator dungeonObstacleGenerator,
    WildernessPopulator wildernessPopulator,
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

        var roster = FactionRosterGenerator.Generate(worldId);
        var namedFactions = roster.JoinableFactions.ToList();

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
        factions.AddRange(roster.AntagonistFactions);
        var encounterFactionsByCreatureType = EncounterFactionGenerator
            .Generate(worldId)
            .ToDictionary();
        encounterFactionsByCreatureType[CreatureType.Human] = roster.BrokenToll;
        factions.AddRange(encounterFactionsByCreatureType.Values);
        var dungeons = new List<DungeonGeneratorResult>();
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
        var doorConnectorLevers = new List<DoorConnectorLever>();
        var relationships = new List<Relationship>();
        var encounterGroups = new List<EncounterGroup>();
        var encounterGroupMembers = new List<EncounterGroupMember>();
        var expeditions = new List<DungeonExpeditionResult>();
        var dungeonInhabitants = new List<DungeonInhabitantResult>();
        var dungeonInhabitantLocationIds = new List<Guid>();

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

            factions.AddRange(cityResult.Factions);
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

        var factionsById = factions.ToDictionary(faction => faction.Id);
        var houseCandidates = factionMembers
            .Where(member => factionsById[member.FactionId].Kind == FactionKind.People)
            .Select(member => member.CreatureId)
            .Distinct()
            .Take(2)
            .ToArray();
        var houseFactions = roster
            .AntagonistFactions.Where(faction =>
                faction.Name is FactionNames.HouseAshvale or FactionNames.HouseMarrow
            )
            .ToArray();
        foreach (var pair in houseFactions.Zip(houseCandidates))
        {
            factionMembers.Add(
                new FactionMember
                {
                    WorldId = worldId,
                    FactionId = pair.First.Id,
                    CreatureId = pair.Second,
                    Role = FactionRole.Leader,
                }
            );
        }

        var monsters = new List<Creature>();
        var creatureSpawners = new List<CreatureSpawner>();
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

            var wildernessGroups = wildernessPopulator.Generate(
                new WildernessPopulatorInput
                {
                    LocationId = wildernessLocation.Id,
                    WorldId = worldId,
                    FactionsByCreatureType = encounterFactionsByCreatureType,
                }
            );
            monsters.AddRange(wildernessGroups.Monsters.Select(monster => monster.Creature));
            items.AddRange(wildernessGroups.Monsters.SelectMany(monster => monster.Items));
            skills.AddRange(wildernessGroups.Monsters.SelectMany(monster => monster.Skills));
            jobs.AddRange(wildernessGroups.Jobs);
            encounterGroups.AddRange(wildernessGroups.EncounterGroups);
            encounterGroupMembers.AddRange(wildernessGroups.EncounterGroupMembers);
            factionMembers.AddRange(wildernessGroups.FactionMembers);
            creatureSpawners.Add(wildernessGroups.Spawner);

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
            var stateDungeons = new List<DungeonGeneratorResult>();
            for (var i = 0; i < count; i++)
            {
                var result = DungeonGenerator.Generate(
                    new DungeonGeneratorInput(usedNames, wildernessLocation, worldId)
                );
                dungeons.Add(result);
                stateDungeons.Add(result);
                usedNames.Add(result.Building.Name);
                buildings.Add(result.Building);
                rooms.AddRange(result.Rooms);
                locations.AddRange(result.Locations);
                locationConnectors.AddRange(result.LocationConnectors);
                doorConnectors.Add(result.Door);

                var inhabitant = dungeonInhabitantGenerator.Generate(
                    new DungeonInhabitantInput(result, Random.Shared)
                );
                if (inhabitant != null)
                {
                    dungeonInhabitants.Add(inhabitant);
                    dungeonInhabitantLocationIds.Add(inhabitant.LocationId);
                }

                // Spread through the dungeon rather than piled at the door, and not into every
                // room: the empty ones are what make walking into an occupied one mean something.
                foreach (var placement in result.Placements)
                {
                    if (placement.Room.LocationId == inhabitant?.LocationId)
                    {
                        continue;
                    }

                    if (!DungeonContentPolicy.HoldsOccupants(placement.Role, Random.Shared))
                    {
                        continue;
                    }

                    var dungeonMonsters = dungeonPopulator.Generate(
                        new DungeonPopulatorInput
                        {
                            LocationId = placement.Room.LocationId,
                            WorldId = worldId,
                            DungeonType = result.Building.BuildingType,
                            FactionsByCreatureType = encounterFactionsByCreatureType,
                        }
                    );
                    monsters.AddRange(dungeonMonsters.Monsters.Select(monster => monster.Creature));
                    items.AddRange(dungeonMonsters.Monsters.SelectMany(monster => monster.Items));
                    skills.AddRange(dungeonMonsters.Monsters.SelectMany(monster => monster.Skills));
                    jobs.AddRange(dungeonMonsters.Jobs);
                    encounterGroups.AddRange(dungeonMonsters.EncounterGroups);
                    encounterGroupMembers.AddRange(dungeonMonsters.EncounterGroupMembers);
                    factionMembers.AddRange(dungeonMonsters.FactionMembers);
                    creatureSpawners.Add(dungeonMonsters.Spawner);
                }

                var dungeonLoot = dungeonLootGenerator.Generate(
                    result.Placements,
                    worldId,
                    Random.Shared
                );
                props.AddRange(dungeonLoot.Containers);
                items.AddRange(dungeonLoot.Items);

                var dungeonTraps = dungeonTrapGenerator.Generate(
                    result.Placements,
                    worldId,
                    wildernessLocation.StateId,
                    Random.Shared
                );
                props.AddRange(dungeonTraps.Traps);
                rooms.AddRange(dungeonTraps.Rooms);
                locations.AddRange(dungeonTraps.Locations);
                locationConnectors.AddRange(dungeonTraps.LocationConnectors);

                var obstacleKind = DungeonObstaclePolicy.ChooseKind(
                    result.Building.BuildingType,
                    Random.Shared
                );
                var obstacle = dungeonObstacleGenerator.Generate(
                    new DungeonObstacleInput(
                        result.Placements,
                        result.LocationConnectors,
                        result.EntranceLocationId,
                        result.BossLocationId,
                        result.Building.Id,
                        result.Building.BuildingType,
                        worldId,
                        wildernessLocation.StateId,
                        encounterFactionsByCreatureType,
                        Random.Shared
                    ),
                    obstacleKind
                );
                doorConnectors.AddRange(obstacle.DoorConnectors);
                items.AddRange(obstacle.Items);
                doorConnectorKeys.AddRange(obstacle.DoorConnectorKeys);
                rooms.AddRange(obstacle.Rooms);
                locations.AddRange(obstacle.Locations);
                locationConnectors.AddRange(obstacle.LocationConnectors);
                monsters.AddRange(obstacle.Monsters.Select(monster => monster.Creature));
                items.AddRange(obstacle.Monsters.SelectMany(monster => monster.Items));
                skills.AddRange(obstacle.Monsters.SelectMany(monster => monster.Skills));
                jobs.AddRange(obstacle.Jobs);
                encounterGroups.AddRange(obstacle.EncounterGroups);
                encounterGroupMembers.AddRange(obstacle.EncounterGroupMembers);
                factionMembers.AddRange(obstacle.FactionMembers);
                creatureSpawners.AddRange(obstacle.CreatureSpawners);
                props.AddRange(obstacle.Traps);
                props.AddRange(obstacle.Levers);
                doorConnectorLevers.AddRange(obstacle.DoorConnectorLevers);

                var shortcutLever = DungeonLeverGenerator.BuildMandatoryShortcutLever(
                    worldId,
                    result.BossLocationId,
                    result.EntranceLocationId,
                    result.BackDoorLocationId,
                    result.LocationConnectors
                );
                props.AddRange(shortcutLever.Levers);
                doorConnectors.AddRange(shortcutLever.DoorConnectors);
                doorConnectorLevers.AddRange(shortcutLever.DoorConnectorLevers);

                if (result.HasLeverGate)
                {
                    var canonicalGate = DungeonLeverGenerator.BuildCanonicalGate(
                        worldId,
                        result.LandingLocationId!.Value,
                        result.BossLocationId,
                        result.LocationConnectors,
                        result.Placements
                    );
                    props.AddRange(canonicalGate.Levers);
                    doorConnectors.AddRange(canonicalGate.DoorConnectors);
                    doorConnectorLevers.AddRange(canonicalGate.DoorConnectorLevers);
                }
            }

            var stateExpedition = dungeonExpeditionGenerator.Generate(
                new DungeonExpeditionInput(
                    stateDungeons,
                    creatureSpawners,
                    props,
                    dungeonInhabitantLocationIds,
                    Random.Shared
                )
            );
            if (stateExpedition != null)
            {
                expeditions.Add(stateExpedition);
                logger.LogInformation(
                    "[expedition] Generated survivor and journal in dungeon {BuildingId}",
                    stateExpedition.Expedition.BuildingId
                );
            }
        }

        foreach (var link in geography.StateTravelLinks)
        {
            AddTravelConnector(link.OriginStateId, link.DestinationStateId, link);
            AddTravelConnector(link.DestinationStateId, link.OriginStateId, link);
        }

        var lairSpecs = new[]
        {
            // Every antagonist-capable faction gets at least one humanoid archetype in its ambient
            // population, and its BossChamber leader is always that humanoid archetype specifically
            // (never sampled from the wider pool) — an antagonist faction is a person leading
            // something, not a monster nest wearing a faction name.
            new AntagonistLairSpec(
                FactionNames.RedTalon,
                BuildingType.Cave,
                "The Red Talon Den",
                [CreatureArchetype.Raider, CreatureArchetype.Beast],
                CreatureArchetype.Raider
            ),
            // "Hunts supernatural threats" means the Vigil's own lodge is staffed by the hunters,
            // not the things they hunt — Undead/Demon/Giant belong to whatever they're chasing,
            // never to their own membership.
            new AntagonistLairSpec(
                FactionNames.SilverVigil,
                BuildingType.Ruins,
                "The Vigil Hunting Lodge",
                [CreatureArchetype.Raider],
                CreatureArchetype.Raider
            ),
            new AntagonistLairSpec(
                FactionNames.CinderPact,
                BuildingType.Tower,
                "The Cinder Spire",
                [CreatureArchetype.Mage, CreatureArchetype.Elemental, CreatureArchetype.Demon],
                CreatureArchetype.Mage
            ),
            new AntagonistLairSpec(
                FactionNames.NightboundCourt,
                BuildingType.Crypt,
                "The Nightbound Crypt",
                [CreatureArchetype.Noble, CreatureArchetype.Undead, CreatureArchetype.Wraith],
                CreatureArchetype.Noble
            ),
            new AntagonistLairSpec(
                FactionNames.AshwoodPack,
                BuildingType.Cave,
                "The Ashwood Den",
                [CreatureArchetype.Raider, CreatureArchetype.Beast],
                CreatureArchetype.Raider
            ),
            new AntagonistLairSpec(
                FactionNames.Reclaimers,
                BuildingType.Mine,
                "The Reclaimer Redoubt",
                [CreatureArchetype.Raider, CreatureArchetype.Construct],
                CreatureArchetype.Raider
            ),
        };
        var antagonistFactionsByName = roster.AntagonistFactions.ToDictionary(faction =>
            faction.Name
        );
        var wildernessLocations = wildernessLocationByStateId.Values.ToArray();
        for (var lairIndex = 0; lairIndex < lairSpecs.Length; lairIndex++)
        {
            var spec = lairSpecs[lairIndex];
            var wildernessLocation = wildernessLocations[lairIndex % wildernessLocations.Length];
            var lair = DungeonGenerator.Generate(
                new DungeonGeneratorInput([], wildernessLocation, worldId)
                {
                    BuildingType = spec.DungeonType,
                    Name = spec.LairName,
                }
            );
            var antagonistFaction = antagonistFactionsByName[spec.FactionName];
            lair.Building.FactionId = antagonistFaction.Id;
            dungeons.Add(lair);
            buildings.Add(lair.Building);
            rooms.AddRange(lair.Rooms);
            locations.AddRange(lair.Locations);
            locationConnectors.AddRange(lair.LocationConnectors);
            doorConnectors.Add(lair.Door);

            foreach (
                var placement in lair.Placements.Where(placement =>
                    DungeonContentPolicy.HoldsOccupants(placement.Role, Random.Shared)
                    || placement.Room.LocationId == lair.BossLocationId
                )
            )
            {
                var population =
                    placement.Room.LocationId == lair.BossLocationId
                        ? dungeonPopulator.GenerateForced(
                            worldId,
                            placement.Room.LocationId,
                            spec.DungeonType,
                            playerLevel: 1,
                            factionsByCreatureType: encounterFactionsByCreatureType,
                            factionId: antagonistFaction.Id,
                            archetypeOverride: [spec.LeaderArchetype]
                        )
                        : dungeonPopulator.Generate(
                            new DungeonPopulatorInput
                            {
                                LocationId = placement.Room.LocationId,
                                WorldId = worldId,
                                DungeonType = spec.DungeonType,
                                FactionsByCreatureType = encounterFactionsByCreatureType,
                                FactionId = antagonistFaction.Id,
                                ArchetypeOverride = spec.Archetypes,
                            }
                        );
                monsters.AddRange(population.Monsters.Select(monster => monster.Creature));
                items.AddRange(population.Monsters.SelectMany(monster => monster.Items));
                skills.AddRange(population.Monsters.SelectMany(monster => monster.Skills));
                jobs.AddRange(population.Jobs);
                encounterGroups.AddRange(population.EncounterGroups);
                encounterGroupMembers.AddRange(population.EncounterGroupMembers);
                factionMembers.AddRange(
                    population.Monsters.Select(monster => new FactionMember
                    {
                        WorldId = worldId,
                        FactionId = antagonistFaction.Id,
                        CreatureId = monster.Creature.Id,
                        Role =
                            placement.Room.LocationId == lair.BossLocationId
                                ? FactionRole.Leader
                                : FactionRole.Member,
                    })
                );
                creatureSpawners.Add(population.Spawner);
            }
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

        var anchoredLocations = LocationCoarseAnchorGenerator.Generate(
            namedLocations,
            geography.Districts,
            wildernessLocationByStateId
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
                Locations = anchoredLocations,
                States = geography.States,
                Countries = geography.Countries,
            }
        );

        creatures.AddRange(monsters);

        var factionStandings = FactionStandingGenerator.Generate(worldId, factions);

        var expeditionParticipantIds = new HashSet<Guid>();
        foreach (var expedition in expeditions)
        {
            creatures.AddRange(expedition.Participants.Select(participant => participant.Creature));
            items.AddRange(expedition.Participants.SelectMany(participant => participant.Items));
            items.Add(expedition.Journal);
            skills.AddRange(expedition.Participants.SelectMany(participant => participant.Skills));
            jobs.AddRange(expedition.Jobs);
            expeditionParticipantIds.UnionWith(
                expedition.Participants.Select(participant => participant.Creature.Id)
            );
        }

        var inhabitantIds = new HashSet<Guid>();
        foreach (var inhabitant in dungeonInhabitants)
        {
            creatures.Add(inhabitant.Participant.Creature);
            items.AddRange(inhabitant.Participant.Items);
            skills.AddRange(inhabitant.Participant.Skills);
            jobs.AddRange(inhabitant.Jobs);
            inhabitantIds.Add(inhabitant.Participant.Creature.Id);
        }

        var excludedFromGenericProfiles = expeditionParticipantIds
            .Concat(inhabitantIds)
            .ToHashSet();
        var creatureProfiles = CreatureProfileGenerator.Generate(
            new CreatureProfileGeneratorInput(
                creatures
                    .Where(creature => !excludedFromGenericProfiles.Contains(creature.Id))
                    .ToArray(),
                anchoredLocations.ToDictionary(location => location.Id),
                factionMembers,
                factions,
                relationships,
                jobs,
                rooms,
                buildings,
                buildingOwners
            )
        );

        var initiationQuests = FactionInitiationQuestGenerator.Generate(
            new FactionInitiationQuestGeneratorInput
            {
                WorldId = worldId,
                Factions = factions,
                FactionMembers = factionMembers,
                Buildings = buildings,
                Rooms = rooms,
                Locations = anchoredLocations,
                Creatures = creatures,
                Props = props,
            }
        );
        items.AddRange(initiationQuests.Items);

        logger.LogDebug("GenerateWorld completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new WorldGeneratorResult
        {
            DungeonExpeditions = expeditions.Select(expedition => expedition.Expedition).ToArray(),
            BookWorks = expeditions.Select(expedition => expedition.Work).ToArray(),
            Facts = expeditions.Select(expedition => expedition.Fact).ToArray(),
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
            FactionStandings = factionStandings,
            InitiationQuests = initiationQuests.Quests.ToArray(),
            InitiationQuestObjectives = initiationQuests.Objectives.ToArray(),
            Buildings = buildings,
            Creatures = creatures,
            BuildingOwners = buildingOwners,
            FactionMembers = factionMembers,
            Items = items,
            Rooms = rooms,
            Locations = anchoredLocations,
            CreatureProfiles =
            [
                .. creatureProfiles,
                .. expeditions.SelectMany(expedition => expedition.Profiles),
                .. dungeonInhabitants.Select(inhabitant => inhabitant.Profile),
            ],
            Props = props,
            Skills = skills,
            Jobs = jobs,
            Knowledge = knowledge,
            DoorConnectorKeys = doorConnectorKeys,
            DoorConnectorLevers = doorConnectorLevers,
            Relationships = relationships,
            CreatureSpawners = creatureSpawners,
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
