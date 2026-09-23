using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class RoadTravelerRouteSeederTests
{
    private static readonly RoadTravelerOptions Options = new()
    {
        SpeedUnitsPerHour = 5,
        LingerHours = 1,
        PilgrimsPerCountry = 1,
        AdventurersPerCountry = 2,
    };

    private readonly RoadTravelerRouteSeeder _seeder = new(
        new CreatureGroupGenerator(Builders.MakeCreatureGenerator())
    );

    [Fact]
    public void Seed_GeneratesPilgrimsAndLoneAdventurers_WithSemanticJourneys()
    {
        var world = BuildWorld();

        var result = _seeder.Seed(world.World, Options);

        Assert.Equal(3, result.Members.Count);
        Assert.Single(
            result.RouteTravelers,
            traveler => traveler.Kind == RouteTravelerKind.Pilgrim
        );
        Assert.Equal(
            2,
            result.RouteTravelers.Count(traveler => traveler.Kind == RouteTravelerKind.Adventurer)
        );
        Assert.Equal(3, result.Creatures.Count);
        Assert.Equal(3, result.Profiles.Count);
        Assert.Contains(
            result.RouteTravelers,
            traveler => traveler.Purpose!.Contains("pilgrimage", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Seed_BuildsRoundTripsThroughEveryRoadLocation()
    {
        var world = BuildWorld();

        var result = _seeder.Seed(world.World, Options);

        foreach (var routeTraveler in result.RouteTravelers)
        {
            var stops = result.Stops.Where(stop => stop.RouteId == routeTraveler.RouteId).ToArray();
            Assert.Contains(stops, stop => stop.LocationId == world.HubLocationId);
            Assert.Equal(4, stops.Length);
        }
    }

    private static RoadTravelerWorldFixture BuildWorld()
    {
        var worldId = Guid.NewGuid();
        var country = new Country
        {
            WorldId = worldId,
            Name = "Valeward",
            DominantRace = CreatureType.Human,
        };
        var state = new State
        {
            WorldId = worldId,
            CountryId = country.Id,
            Name = "Heartland",
        };
        var hub = new Location
        {
            WorldId = worldId,
            StateId = state.Id,
            Kind = LocationKind.Wilderness,
            Name = "Old King's Road",
        };
        var cities = new[]
        {
            new City
            {
                WorldId = worldId,
                CountryId = country.Id,
                Name = "Ashford",
                IsCapital = true,
            },
            new City
            {
                WorldId = worldId,
                CountryId = country.Id,
                Name = "Westmere",
            },
        };
        var entrances = cities
            .Select(city => new Location
            {
                WorldId = worldId,
                StateId = state.Id,
                CityId = city.Id,
                Kind = LocationKind.District,
                Name = $"{city.Name} Gate",
            })
            .ToArray();
        var districts = cities
            .Select(
                (city, index) =>
                    new District
                    {
                        WorldId = worldId,
                        CityId = city.Id,
                        LocationId = entrances[index].Id,
                        DistrictType = DistrictType.CityEntrance,
                        Name = entrances[index].Name,
                    }
            )
            .ToArray();
        var buildings = cities
            .Select(
                (city, index) =>
                    new Building
                    {
                        WorldId = worldId,
                        ExteriorLocationId = entrances[index].Id,
                        BuildingType = BuildingType.Temple,
                        Name = $"Temple of {city.Name}",
                    }
            )
            .ToArray();
        var locationConnectors = new List<LocationConnector>();
        var travelConnectors = new List<TravelConnector>();
        foreach (var entrance in entrances)
        {
            AddRoad(worldId, entrance.Id, hub.Id, locationConnectors, travelConnectors);
        }

        return new RoadTravelerWorldFixture(
            new WorldGeneratorResult
            {
                World = new World { Id = worldId },
                Countries = [country],
                States = [state],
                Cities = cities,
                Districts = districts,
                Locations = [hub, .. entrances],
                LocationConnectors = locationConnectors,
                TravelConnectors = travelConnectors,
                Buildings = buildings,
                InitiationQuests = [],
                InitiationQuestObjectives = [],
                FactionStandings = [],
                BuildingOwners = [],
                Creatures = [],
                EncounterGroups = [],
                EncounterGroupMembers = [],
                FactionMembers = [],
                Factions = [],
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
            },
            hub.Id
        );
    }

    private static void AddRoad(
        Guid worldId,
        Guid firstLocationId,
        Guid secondLocationId,
        List<LocationConnector> locationConnectors,
        List<TravelConnector> travelConnectors
    )
    {
        foreach (
            var (origin, destination) in new[]
            {
                (firstLocationId, secondLocationId),
                (secondLocationId, firstLocationId),
            }
        )
        {
            var connector = new LocationConnector
            {
                WorldId = worldId,
                OriginLocationId = origin,
                DestinationLocationId = destination,
                DestinationLabel = "Road",
            };
            locationConnectors.Add(connector);
            travelConnectors.Add(
                new TravelConnector
                {
                    WorldId = worldId,
                    ConnectorId = connector.Id,
                    Distance = 5,
                }
            );
        }
    }

    private sealed record RoadTravelerWorldFixture(WorldGeneratorResult World, Guid HubLocationId);
}
