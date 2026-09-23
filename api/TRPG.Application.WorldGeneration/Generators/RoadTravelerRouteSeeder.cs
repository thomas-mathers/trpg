using TRPG.Application.Configuration;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public record RoadTravelerRouteSeederResult(
    IReadOnlyList<Route> Routes,
    IReadOnlyList<RouteStop> Stops,
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
    RouteTravelerKind Kind,
    CityRoadStop Origin,
    CityRoadStop Destination,
    IReadOnlyDictionary<Guid, List<(Guid Neighbor, float Distance)>> Adjacency,
    RoadTravelerOptions Options,
    int SequenceIndex,
    int TravelerCount
);

internal record CityRoadStop(City City, Guid LocationId, string? TempleName);

internal record RoadPathNode(Guid LocationId, float DistanceFromPrevious);

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
        var adjacency = CaravanRouteSeeder.BuildTravelAdjacency(world);
        var countryIdByLocationId = BuildCountryIdByLocationId(world);
        var cityStops = BuildCityStops(world);

        foreach (var country in world.Countries)
        {
            var stops = cityStops.Where(s => s.City.CountryId == country.Id).ToArray();
            if (stops.Length < 2)
            {
                continue;
            }

            var countryAdjacency = FilterToCountry(adjacency, countryIdByLocationId, country.Id);
            results.AddRange(SeedPilgrims(world, country, stops, countryAdjacency, options));
            results.AddRange(SeedAdventurers(world, country, stops, countryAdjacency, options));
        }

        var creatures = results.Select(result => result.Creature.Creature).ToArray();
        var profiles = GenerateProfiles(world, creatures);
        return new RoadTravelerRouteSeederResult(
            results.Select(result => result.Route).ToArray(),
            results.SelectMany(result => result.Stops).ToArray(),
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
        IReadOnlyDictionary<Guid, List<(Guid Neighbor, float Distance)>> adjacency,
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
                        RouteTravelerKind.Pilgrim,
                        origin,
                        destination,
                        adjacency,
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
        IReadOnlyDictionary<Guid, List<(Guid Neighbor, float Distance)>> adjacency,
        RoadTravelerOptions options
    ) =>
        Enumerable
            .Range(0, options.AdventurersPerCountry)
            .Select(index =>
                SeedTraveler(
                    new RoadTravelerSeedInput(
                        world,
                        country,
                        RouteTravelerKind.Adventurer,
                        stops[index % stops.Count],
                        stops[(index + 1) % stops.Count],
                        adjacency,
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
        var path = FindPath(input.Adjacency, input.Origin.LocationId, input.Destination.LocationId);
        if (path.Count < 2)
        {
            return null;
        }

        var routeId = Guid.NewGuid();
        var stops = BuildRoundTripStops(path, input.Adjacency, routeId);
        var route = new Route
        {
            Id = routeId,
            WorldId = input.World.World.Id,
            Name = $"{input.Origin.City.Name} to {input.Destination.City.Name} journey",
            LingerHours = input.Options.LingerHours,
        };
        var routeTraveler = BuildRouteTraveler(input, route, stops);
        var creature = GenerateCreature(input, stops[0].LocationId);
        var member = new RouteTravelerMember
        {
            WorldId = input.World.World.Id,
            RouteTravelerId = routeTraveler.Id,
            CreatureId = creature.Creature.Id,
        };

        return new SeededRoadTraveler(route, stops, routeTraveler, member, creature);
    }

    private static RouteTraveler BuildRouteTraveler(
        RoadTravelerSeedInput input,
        Route route,
        IReadOnlyList<RouteStop> stops
    )
    {
        var waypoints = stops
            .Select(stop => new RouteWaypoint(stop.LocationId, stop.DistanceToNextStop))
            .ToArray();
        var cycleHours = RouteCycle.TotalCycleHours(
            waypoints,
            route.LingerHours,
            input.Options.SpeedUnitsPerHour
        );
        return new RouteTraveler
        {
            WorldId = input.World.World.Id,
            RouteId = route.Id,
            Direction = RouteDirection.Clockwise,
            PhaseOffsetHours = cycleHours * input.SequenceIndex / input.TravelerCount,
            Kind = input.Kind,
            Purpose = BuildPurpose(input),
        };
    }

    private CreatureGeneratorResult GenerateCreature(RoadTravelerSeedInput input, Guid locationId)
    {
        var profession =
            input.Kind == RouteTravelerKind.Pilgrim
                ? Profession.Cleric
                : AdventurerProfessions[input.SequenceIndex % AdventurerProfessions.Length];
        var minimumLevel =
            input.Kind == RouteTravelerKind.Pilgrim
                ? input.Options.MinimumPilgrimLevel
                : input.Options.MinimumAdventurerLevel;
        var maximumLevel =
            input.Kind == RouteTravelerKind.Pilgrim
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
        input.Kind == RouteTravelerKind.Pilgrim
            ? $"Making a pilgrimage to {input.Destination.TempleName} in {input.Destination.City.Name}."
            : $"Traveling to {input.Destination.City.Name} in search of work, rumors, and adventure.";

    private static IReadOnlyList<RouteStop> BuildRoundTripStops(
        IReadOnlyList<RoadPathNode> path,
        IReadOnlyDictionary<Guid, List<(Guid Neighbor, float Distance)>> adjacency,
        Guid routeId
    )
    {
        var locationIds = path.Select(node => node.LocationId)
            .Concat(path.Skip(1).SkipLast(1).Reverse().Select(node => node.LocationId))
            .ToArray();
        return locationIds
            .Select(
                (locationId, index) =>
                    new RouteStop
                    {
                        RouteId = routeId,
                        SequenceIndex = index,
                        LocationId = locationId,
                        DistanceToNextStop = FindDistance(
                            adjacency,
                            locationId,
                            locationIds[(index + 1) % locationIds.Length]
                        ),
                    }
            )
            .ToArray();
    }

    private static IReadOnlyList<RoadPathNode> FindPath(
        IReadOnlyDictionary<Guid, List<(Guid Neighbor, float Distance)>> adjacency,
        Guid origin,
        Guid destination
    )
    {
        var cameFrom = new Dictionary<Guid, (Guid Parent, float Distance)>
        {
            [origin] = (origin, 0),
        };
        var queue = new Queue<Guid>();
        queue.Enqueue(origin);
        while (queue.Count > 0 && !cameFrom.ContainsKey(destination))
        {
            var current = queue.Dequeue();
            foreach (var edge in adjacency.GetValueOrDefault(current, []))
            {
                if (cameFrom.TryAdd(edge.Neighbor, (current, edge.Distance)))
                {
                    queue.Enqueue(edge.Neighbor);
                }
            }
        }

        return ReconstructPath(cameFrom, origin, destination);
    }

    private static IReadOnlyList<RoadPathNode> ReconstructPath(
        IReadOnlyDictionary<Guid, (Guid Parent, float Distance)> cameFrom,
        Guid origin,
        Guid destination
    )
    {
        if (!cameFrom.ContainsKey(destination))
        {
            return [];
        }

        var path = new List<RoadPathNode>();
        for (var node = destination; ; node = cameFrom[node].Parent)
        {
            path.Add(new RoadPathNode(node, cameFrom[node].Distance));
            if (node == origin)
            {
                break;
            }
        }
        path.Reverse();
        return path;
    }

    private static float FindDistance(
        IReadOnlyDictionary<Guid, List<(Guid Neighbor, float Distance)>> adjacency,
        Guid origin,
        Guid destination
    ) => adjacency[origin].First(edge => edge.Neighbor == destination).Distance;

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

    private static IReadOnlyDictionary<Guid, List<(Guid Neighbor, float Distance)>> FilterToCountry(
        IReadOnlyDictionary<Guid, List<(Guid Neighbor, float Distance)>> adjacency,
        IReadOnlyDictionary<Guid, Guid> countryIdByLocationId,
        Guid countryId
    ) =>
        adjacency
            .Where(pair => countryIdByLocationId.GetValueOrDefault(pair.Key) == countryId)
            .ToDictionary(
                pair => pair.Key,
                pair =>
                    pair.Value.Where(edge =>
                            countryIdByLocationId.GetValueOrDefault(edge.Neighbor) == countryId
                        )
                        .ToList()
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
        IReadOnlyList<RouteStop> Stops,
        RouteTraveler RouteTraveler,
        RouteTravelerMember Member,
        CreatureGeneratorResult Creature
    );
}
