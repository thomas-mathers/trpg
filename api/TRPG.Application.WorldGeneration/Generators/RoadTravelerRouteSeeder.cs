using TRPG.Application.Configuration;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public record RoadTravelerRouteSeederResult(
    IReadOnlyList<Route> Routes,
    IReadOnlyList<RouteStep> Steps,
    IReadOnlyList<RouteTraveler> RouteTravelers,
    IReadOnlyList<RouteTravelerMember> Members,
    IReadOnlyList<Creature> Creatures,
    IReadOnlyList<Item> Items,
    IReadOnlyList<CreatureSkill> Skills,
    IReadOnlyList<CreatureProfile> Profiles
);

internal record RoadTravelerSeedInput(
    WorldGeneratorResult World,
    Country Country,
    RoadTravelerType Type,
    CityRoadStop Origin,
    CityRoadStop Destination,
    IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> Graph,
    RoadTravelerOptions Options,
    int SequenceIndex,
    int TravelerCount
);

internal enum RoadTravelerType
{
    Pilgrim,
    Adventurer,
}

internal record CityRoadStop(City City, Guid LocationId, string? TempleName);

public class RoadTravelerRouteSeeder(CreatureGroupGenerator creatureGroupGenerator)
{
    private static readonly Profession[] AdventurerProfessions =
    [
        Profession.Ranger,
        Profession.Mercenary,
        Profession.Rogue,
        Profession.Mage,
        Profession.Knight,
        Profession.Cleric,
    ];

    public RoadTravelerRouteSeederResult Seed(
        WorldGeneratorResult world,
        RoadTravelerOptions options
    )
    {
        var results = new List<SeededRoadTraveler>();
        var graph = TravelGraph.Build(world);
        var countryIdByLocationId = BuildCountryIdByLocationId(world);
        var cityStops = BuildCityStops(world);

        foreach (var country in world.Countries)
        {
            var stops = cityStops.Where(s => s.City.CountryId == country.Id).ToArray();
            if (stops.Length < 2)
            {
                continue;
            }

            var countryGraph = FilterToCountry(graph, countryIdByLocationId, country.Id);
            results.AddRange(SeedPilgrims(world, country, stops, countryGraph, options));
            results.AddRange(SeedAdventurers(world, country, stops, countryGraph, options));
        }

        var creatures = results.Select(result => result.Creature.Creature).ToArray();
        var profiles = GenerateProfiles(world, creatures);
        return new RoadTravelerRouteSeederResult(
            results.Select(result => result.Route).ToArray(),
            results.SelectMany(result => result.Steps).ToArray(),
            results.Select(result => result.RouteTraveler).ToArray(),
            results.Select(result => result.Member).ToArray(),
            creatures,
            results.SelectMany(result => result.Creature.Items).ToArray(),
            results.SelectMany(result => result.Creature.Skills).ToArray(),
            profiles
        );
    }

    private IReadOnlyList<SeededRoadTraveler> SeedPilgrims(
        WorldGeneratorResult world,
        Country country,
        IReadOnlyList<CityRoadStop> stops,
        IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> graph,
        RoadTravelerOptions options
    )
    {
        var templeStops = stops.Where(stop => stop.TempleName != null).ToArray();
        if (templeStops.Length == 0)
        {
            return [];
        }

        return Enumerable
            .Range(0, options.PilgrimsPerCountry)
            .Select(index =>
            {
                var destination = templeStops[index % templeStops.Length];
                var origin = stops.First(stop => stop.City.Id != destination.City.Id);
                return SeedTraveler(
                    new RoadTravelerSeedInput(
                        world,
                        country,
                        RoadTravelerType.Pilgrim,
                        origin,
                        destination,
                        graph,
                        options,
                        index,
                        options.PilgrimsPerCountry
                    )
                );
            })
            .Where(result => result != null)
            .Select(result => result!)
            .ToArray();
    }

    private IReadOnlyList<SeededRoadTraveler> SeedAdventurers(
        WorldGeneratorResult world,
        Country country,
        IReadOnlyList<CityRoadStop> stops,
        IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> graph,
        RoadTravelerOptions options
    ) =>
        Enumerable
            .Range(0, options.AdventurersPerCountry)
            .Select(index =>
                SeedTraveler(
                    new RoadTravelerSeedInput(
                        world,
                        country,
                        RoadTravelerType.Adventurer,
                        stops[index % stops.Count],
                        stops[(index + 1) % stops.Count],
                        graph,
                        options,
                        index,
                        options.AdventurersPerCountry
                    )
                )
            )
            .Where(result => result != null)
            .Select(result => result!)
            .ToArray();

    private SeededRoadTraveler? SeedTraveler(RoadTravelerSeedInput input)
    {
        var forwardPath = TravelGraph.FindShortestPath(
            input.Graph,
            input.Origin.LocationId,
            input.Destination.LocationId
        );
        var returnPath = TravelGraph.FindShortestPath(
            input.Graph,
            input.Destination.LocationId,
            input.Origin.LocationId
        );
        if (forwardPath.Count == 0 || returnPath.Count == 0)
        {
            return null;
        }

        var routeId = Guid.NewGuid();
        var seededSteps = BuildRoundTripSteps(
            forwardPath.Concat(returnPath).ToArray(),
            input,
            routeId
        );
        var route = new Route
        {
            Id = routeId,
            WorldId = input.World.World.Id,
            Name = $"{input.Origin.City.Name} to {input.Destination.City.Name} journey",
            Traversal = RouteTraversal.Cyclic,
        };
        var routeTraveler = BuildRouteTraveler(input, route, seededSteps.DurationHours);
        var creature = GenerateCreature(input, seededSteps.Steps[0].LocationId);
        var member = new RouteTravelerMember
        {
            WorldId = input.World.World.Id,
            RouteTravelerId = routeTraveler.Id,
            CreatureId = creature.Creature.Id,
        };

        return new SeededRoadTraveler(route, seededSteps.Steps, routeTraveler, member, creature);
    }

    private static RouteTraveler BuildRouteTraveler(
        RoadTravelerSeedInput input,
        Route route,
        double durationHours
    )
    {
        return new RouteTraveler
        {
            WorldId = input.World.World.Id,
            RouteId = route.Id,
            StartedAtPlaytime =
                -GameClock.RealTimePerInGameHour
                * durationHours
                * input.SequenceIndex
                / input.TravelerCount,
            SpeedUnitsPerHour = input.Options.SpeedUnitsPerHour,
            Purpose = BuildPurpose(input),
        };
    }

    private CreatureGeneratorResult GenerateCreature(RoadTravelerSeedInput input, Guid locationId)
    {
        var profession =
            input.Type == RoadTravelerType.Pilgrim
                ? Profession.Cleric
                : AdventurerProfessions[input.SequenceIndex % AdventurerProfessions.Length];
        var minimumLevel =
            input.Type == RoadTravelerType.Pilgrim
                ? input.Options.MinimumPilgrimLevel
                : input.Options.MinimumAdventurerLevel;
        var maximumLevel =
            input.Type == RoadTravelerType.Pilgrim
                ? input.Options.MaximumPilgrimLevel
                : input.Options.MaximumAdventurerLevel;

        return creatureGroupGenerator
            .Generate(
                new CreatureGroupGeneratorInput(
                    input.Country.DominantRace,
                    profession,
                    input.World.World.Id,
                    locationId,
                    Count: 1,
                    MinLevel: minimumLevel,
                    MaxLevel: maximumLevel
                )
            )
            .Single();
    }

    private static string BuildPurpose(RoadTravelerSeedInput input) =>
        input.Type == RoadTravelerType.Pilgrim
            ? $"Making a pilgrimage to {input.Destination.TempleName} in {input.Destination.City.Name}."
            : $"Traveling to {input.Destination.City.Name} in search of work, rumors, and adventure.";

    private static SeededRouteSteps BuildRoundTripSteps(
        IReadOnlyList<TravelPathLeg> legs,
        RoadTravelerSeedInput input,
        Guid routeId
    )
    {
        var steps = legs.Select(
                (leg, index) =>
                    new RouteStep
                    {
                        WorldId = input.World.World.Id,
                        RouteId = routeId,
                        SequenceIndex = index,
                        LocationId = leg.OriginLocationId,
                        ConnectorId = leg.ConnectorId,
                        DwellHours = input.Options.LingerHours,
                    }
            )
            .ToArray();
        var durationHours = legs.Sum(leg =>
            input.Options.LingerHours + leg.Distance / input.Options.SpeedUnitsPerHour
        );
        return new SeededRouteSteps(steps, durationHours);
    }

    private static IReadOnlyList<CityRoadStop> BuildCityStops(WorldGeneratorResult world)
    {
        var locationsById = world.Locations.ToDictionary(location => location.Id);
        var templeByCityId = world
            .Buildings.Where(building => building.BuildingType == BuildingType.Temple)
            .Where(building => locationsById[building.ExteriorLocationId].CityId != null)
            .GroupBy(building => locationsById[building.ExteriorLocationId].CityId!.Value)
            .ToDictionary(group => group.Key, group => group.First().Name);
        var entrancesByCityId = world
            .Districts.Where(district => district.DistrictType == DistrictType.CityEntrance)
            .ToDictionary(district => district.CityId, district => district.LocationId);

        return world
            .Cities.Where(city => entrancesByCityId.ContainsKey(city.Id))
            .Select(city => new CityRoadStop(
                city,
                entrancesByCityId[city.Id],
                templeByCityId.GetValueOrDefault(city.Id)
            ))
            .ToArray();
    }

    private static IReadOnlyDictionary<Guid, Guid> BuildCountryIdByLocationId(
        WorldGeneratorResult world
    )
    {
        var countryIdByStateId = world.States.ToDictionary(
            state => state.Id,
            state => state.CountryId
        );
        return world.Locations.ToDictionary(
            location => location.Id,
            location => countryIdByStateId[location.StateId]
        );
    }

    private static IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> FilterToCountry(
        IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> graph,
        IReadOnlyDictionary<Guid, Guid> countryIdByLocationId,
        Guid countryId
    ) =>
        graph
            .Where(pair => countryIdByLocationId.GetValueOrDefault(pair.Key) == countryId)
            .ToDictionary(
                pair => pair.Key,
                pair =>
                    (IReadOnlyList<TravelGraphEdge>)
                        pair
                            .Value.Where(edge =>
                                countryIdByLocationId.GetValueOrDefault(edge.DestinationLocationId)
                                == countryId
                            )
                            .ToArray()
            );

    private static IReadOnlyList<CreatureProfile> GenerateProfiles(
        WorldGeneratorResult world,
        IReadOnlyCollection<Creature> creatures
    ) =>
        CreatureProfileGenerator.Generate(
            new CreatureProfileGeneratorInput(
                creatures,
                world.Locations.ToDictionary(location => location.Id),
                [],
                world.Factions,
                [],
                [],
                world.Rooms,
                world.Buildings,
                []
            )
        );

    private record SeededRoadTraveler(
        Route Route,
        IReadOnlyList<RouteStep> Steps,
        RouteTraveler RouteTraveler,
        RouteTravelerMember Member,
        CreatureGeneratorResult Creature
    );

    private record SeededRouteSteps(IReadOnlyList<RouteStep> Steps, double DurationHours);
}
