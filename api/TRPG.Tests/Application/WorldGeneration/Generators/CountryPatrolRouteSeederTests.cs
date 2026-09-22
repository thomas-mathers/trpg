using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class CountryPatrolRouteSeederTests
{
    private static readonly CountryPatrolOptions Options = new()
    {
        SpeedUnitsPerHour = 5,
        DefaultLingerHours = 1,
        SquadSize = 3,
        MinGuardLevel = 25,
        MaxGuardLevel = 50,
    };

    private readonly CountryPatrolRouteSeeder _seeder = new(
        new CreatureGroupGenerator(Builders.MakeCreatureGenerator())
    );

    [Fact]
    public void Seed_BuildsOneRoutePerCountry_LingeringOnlyAtThatCountrysOwnWilderness()
    {
        var worldId = Guid.NewGuid();
        var (world, entranceLocationIdsByCountryId, hubLocationIdByCountryId) =
            BuildTwoCountryWorld(worldId);
        var everyEntranceLocationId = entranceLocationIdsByCountryId
            .Values.SelectMany(ids => ids)
            .ToHashSet();

        var result = _seeder.Seed(world, Options);

        Assert.Equal(2, result.Routes.Count);
        foreach (var (countryId, expectedLocationIds) in entranceLocationIdsByCountryId)
        {
            var country = world.Countries.Single(c => c.Id == countryId);
            var route = result.Routes.Single(r => r.Name == $"{country.Name} Road Patrol");
            var stops = result.Stops.Where(s => s.RouteId == route.Id).ToArray();

            // Every leg in this fixture's star topology has to pass through that country's own
            // single shared hub, so the loop lingers there once per city-to-city leg — never at a
            // city gate, and never at the other country's hub either.
            Assert.Equal(expectedLocationIds.Count, stops.Length);
            Assert.All(
                stops,
                stop => Assert.Equal(hubLocationIdByCountryId[countryId], stop.LocationId)
            );
            Assert.DoesNotContain(stops, stop => everyEntranceLocationId.Contains(stop.LocationId));
        }
    }

    [Fact]
    public void Seed_GeneratesOneSquadPerCountry_LinkedToItsOwnPatrolTraveler()
    {
        var worldId = Guid.NewGuid();
        var (world, _, _) = BuildTwoCountryWorld(worldId);

        var result = _seeder.Seed(world, Options);

        Assert.Equal(2, result.Travelers.Count);
        Assert.Equal(Options.SquadSize * 2, result.Creatures.Count);
        Assert.Equal(Options.SquadSize * 2, result.Members.Count);

        foreach (var traveler in result.Travelers)
        {
            var memberCreatureIds = result
                .Members.Where(m => m.RouteTravelerId == traveler.Id)
                .Select(m => m.CreatureId)
                .ToArray();
            Assert.Equal(Options.SquadSize, memberCreatureIds.Length);
            Assert.All(
                memberCreatureIds,
                creatureId => Assert.Contains(result.Creatures, c => c.Id == creatureId)
            );
        }
    }

    [Fact]
    public void Seed_LinksEveryGuard_ToTheirCountrysCapitalGuardFaction()
    {
        var worldId = Guid.NewGuid();
        var (world, _, _) = BuildTwoCountryWorld(worldId);

        var result = _seeder.Seed(world, Options);

        var guardFactionIds = world
            .Factions.Where(f => f.Kind == FactionKind.CityGuard)
            .Select(f => f.Id)
            .ToHashSet();
        Assert.Equal(Options.SquadSize * 2, result.FactionMembers.Count);
        Assert.All(
            result.FactionMembers,
            member => Assert.Contains(member.FactionId, guardFactionIds)
        );
    }

    [Fact]
    public void Seed_SkipsACountry_WhenFewerThanTwoOfItsCitiesHaveAnEntrance()
    {
        var worldId = Guid.NewGuid();
        var (world, _, _) = BuildTwoCountryWorld(worldId, secondCountryCityCount: 1);

        var result = _seeder.Seed(world, Options);

        Assert.Single(result.Routes);
        Assert.Single(result.Travelers);
        Assert.Equal(Options.SquadSize, result.Creatures.Count);
    }

    // Each country gets its own isolated star: every one of its cities connects only to that
    // country's own hub, so there is no cross-country edge at all — this proves the seeder scopes
    // a country's route to its own cities without needing a shared-hub topology to prove it.
    private static (
        WorldGeneratorResult World,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> EntranceLocationIdsByCountryId,
        IReadOnlyDictionary<Guid, Guid> HubLocationIdByCountryId
    ) BuildTwoCountryWorld(Guid worldId, int secondCountryCityCount = 3)
    {
        var countries = new List<Country>();
        var states = new List<State>();
        var cities = new List<City>();
        var districts = new List<District>();
        var locations = new List<Location>();
        var locationConnectors = new List<LocationConnector>();
        var travelConnectors = new List<TravelConnector>();
        var factions = new List<Faction>();
        var entranceLocationIdsByCountryId = new Dictionary<Guid, IReadOnlyList<Guid>>();
        var hubLocationIdByCountryId = new Dictionary<Guid, Guid>();

        foreach (var cityCount in new[] { 3, secondCountryCityCount })
        {
            var country = new Country
            {
                Id = Guid.NewGuid(),
                WorldId = worldId,
                Name = $"Country-{Guid.NewGuid():N}",
                DominantRace = CreatureType.Human,
            };
            countries.Add(country);

            // One shared state per country is enough here: every location this fixture creates
            // just needs to resolve back to this country through its StateId, which is all the
            // seeder's same-country adjacency filter looks at.
            var state = new State
            {
                Id = Guid.NewGuid(),
                WorldId = worldId,
                CountryId = country.Id,
            };
            states.Add(state);

            var hubLocation = new Location
            {
                Id = Guid.NewGuid(),
                WorldId = worldId,
                StateId = state.Id,
            };
            locations.Add(hubLocation);
            hubLocationIdByCountryId[country.Id] = hubLocation.Id;

            var entranceLocationIds = new List<Guid>();
            for (var i = 0; i < cityCount; i++)
            {
                var city = new City
                {
                    Id = Guid.NewGuid(),
                    WorldId = worldId,
                    CountryId = country.Id,
                    IsCapital = i == 0,
                };
                cities.Add(city);

                if (i == 0)
                {
                    factions.Add(
                        new Faction
                        {
                            WorldId = worldId,
                            CityId = city.Id,
                            Kind = FactionKind.CityGuard,
                            IsCityFaction = true,
                            Name = $"{city.Name} City Guard",
                        }
                    );
                }

                var entranceLocation = new Location
                {
                    Id = Guid.NewGuid(),
                    WorldId = worldId,
                    StateId = state.Id,
                };
                var entranceDistrict = new District
                {
                    Id = Guid.NewGuid(),
                    WorldId = worldId,
                    CityId = city.Id,
                    DistrictType = DistrictType.CityEntrance,
                    LocationId = entranceLocation.Id,
                };
                locations.Add(entranceLocation);
                districts.Add(entranceDistrict);
                entranceLocationIds.Add(entranceLocation.Id);

                AddBidirectionalConnector(
                    worldId,
                    entranceLocation.Id,
                    hubLocation.Id,
                    distance: 5,
                    locationConnectors,
                    travelConnectors
                );
            }

            entranceLocationIdsByCountryId[country.Id] = entranceLocationIds;
        }

        var world = new WorldGeneratorResult
        {
            World = new World { Id = worldId },
            Countries = countries,
            States = states,
            Cities = cities,
            Districts = districts,
            Locations = locations,
            LocationConnectors = locationConnectors,
            TravelConnectors = travelConnectors,
            InitiationQuests = [],
            InitiationQuestObjectives = [],
            FactionStandings = [],
            BuildingOwners = [],
            Buildings = [],
            Creatures = [],
            EncounterGroups = [],
            EncounterGroupMembers = [],
            FactionMembers = [],
            Factions = factions,
            Items = [],
            Jobs = [],
            Knowledge = [],
            CreatureProfiles = [],
            Props = [],
            Relationships = [],
            DoorConnectors = [],
            DoorConnectorKeys = [],
            DoorConnectorLevers = [],
            Rooms = [],
            Skills = [],
            CreatureSpawners = [],
        };

        return (world, entranceLocationIdsByCountryId, hubLocationIdByCountryId);
    }

    private static void AddBidirectionalConnector(
        Guid worldId,
        Guid originLocationId,
        Guid destinationLocationId,
        float distance,
        List<LocationConnector> locationConnectors,
        List<TravelConnector> travelConnectors
    )
    {
        foreach (
            var (from, to) in new[]
            {
                (originLocationId, destinationLocationId),
                (destinationLocationId, originLocationId),
            }
        )
        {
            var connector = new LocationConnector
            {
                OriginLocationId = from,
                DestinationLocationId = to,
                DestinationLabel = "",
                WorldId = worldId,
            };
            locationConnectors.Add(connector);
            travelConnectors.Add(
                new TravelConnector
                {
                    ConnectorId = connector.Id,
                    Distance = distance,
                    WorldId = worldId,
                }
            );
        }
    }
}
