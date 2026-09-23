using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class CaravanRouteSeederTests
{
    private static readonly CaravanOptions Options = new()
    {
        SpeedUnitsPerHour = 5,
        DefaultLingerHours = 1,
        CaravansPerStop = 1,
        DefaultTicketFeeGold = 10,
    };

    [Fact]
    public void Seed_BuildsALoopThroughEveryCapital_WhenAtLeastTwoCountriesExist()
    {
        var worldId = Guid.NewGuid();
        var (world, capitalLocationIds) = BuildStarTopologyWorld(worldId, countryCount: 3);

        var result = CaravanRouteSeeder.Seed(world, Options);

        Assert.Equal(2, result.Routes.Count);
        Assert.Equal(12, result.Steps.Count);
        Assert.Equal(
            capitalLocationIds.OrderBy(id => id),
            result
                .Steps.Where(step => step.DwellHours > 0)
                .Select(step => step.LocationId)
                .Distinct()
                .OrderBy(id => id)
        );
        Assert.All(result.Steps, step => Assert.NotNull(step.ConnectorId));
    }

    [Fact]
    public void Seed_SpawnsEvenlyPhaseOffsetCaravans_ForTheConfiguredCount()
    {
        var worldId = Guid.NewGuid();
        var (world, _) = BuildStarTopologyWorld(worldId, countryCount: 3);

        var result = CaravanRouteSeeder.Seed(world, Options);

        // 3 stops * (1 linger hour + 2 leg hours) = 9 total cycle hours. CaravansPerStop = 1 gives
        // 3 instances per direction (one per stop), so 6 total — one clockwise and one
        // counter-clockwise anchored to each capital.
        Assert.Equal(3 * 2, result.Travelers.Count);
        Assert.Equal(
            [-6d, -6d, -3d, -3d, 0d, 0d],
            result
                .Travelers.Select(traveler =>
                    traveler.StartedAtPlaytime / GameClock.RealTimePerInGameHour
                )
                .OrderBy(hours => hours)
        );
        Assert.All(
            result.Routes,
            route => Assert.Equal(3, result.Travelers.Count(t => t.RouteId == route.Id))
        );
    }

    [Fact]
    public void Seed_BuildsAScheduleSignAtEveryStop()
    {
        var worldId = Guid.NewGuid();
        var (world, capitalLocationIds) = BuildStarTopologyWorld(worldId, countryCount: 3);

        var result = CaravanRouteSeeder.Seed(world, Options);

        Assert.Equal(capitalLocationIds.Count, result.Signs.Count);
        Assert.Equal(
            capitalLocationIds.OrderBy(id => id),
            result.Signs.Select(sign => sign.LocationId).OrderBy(id => id)
        );
        Assert.All(result.Signs, sign => Assert.Equal(worldId, sign.WorldId));
        Assert.All(result.Signs, sign => Assert.NotEmpty(sign.Description));
    }

    [Fact]
    public void Seed_ReturnsNoRoute_WhenFewerThanTwoCapitalsExist()
    {
        var worldId = Guid.NewGuid();
        var (world, _) = BuildStarTopologyWorld(worldId, countryCount: 1);

        var result = CaravanRouteSeeder.Seed(world, Options);

        Assert.Empty(result.Routes);
        Assert.Empty(result.Steps);
        Assert.Empty(result.Travelers);
        Assert.Empty(result.Signs);
    }

    // Every capital's entrance connects only to a single shared hub location, so every capital
    // pair is exactly one hop-and-back apart — a tree topology (matching production's
    // state-hub graph) that also keeps the expected inter-capital distance identical no matter
    // which order the seeder's DFS happens to visit them in.
    private static (
        WorldGeneratorResult World,
        IReadOnlyList<Guid> CapitalLocationIds
    ) BuildStarTopologyWorld(Guid worldId, int countryCount)
    {
        var hubLocation = new Location { Id = Guid.NewGuid(), WorldId = worldId };

        var countries = new List<Country>();
        var cities = new List<City>();
        var districts = new List<District>();
        var locations = new List<Location> { hubLocation };
        var locationConnectors = new List<LocationConnector>();
        var travelConnectors = new List<TravelConnector>();
        var capitalLocationIds = new List<Guid>();

        for (var i = 0; i < countryCount; i++)
        {
            var country = new Country { Id = Guid.NewGuid(), WorldId = worldId };
            var city = new City
            {
                Id = Guid.NewGuid(),
                WorldId = worldId,
                CountryId = country.Id,
                IsCapital = true,
            };
            var entranceLocation = new Location { Id = Guid.NewGuid(), WorldId = worldId };
            var entranceDistrict = new District
            {
                Id = Guid.NewGuid(),
                WorldId = worldId,
                CityId = city.Id,
                DistrictType = DistrictType.CityEntrance,
                LocationId = entranceLocation.Id,
            };

            countries.Add(country);
            cities.Add(city);
            locations.Add(entranceLocation);
            districts.Add(entranceDistrict);
            capitalLocationIds.Add(entranceLocation.Id);

            AddBidirectionalConnector(
                worldId,
                entranceLocation.Id,
                hubLocation.Id,
                distance: 5,
                locationConnectors,
                travelConnectors
            );
        }

        var world = new WorldGeneratorResult
        {
            World = new World { Id = worldId },
            Countries = countries,
            States = [],
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
        };

        return (world, capitalLocationIds);
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
