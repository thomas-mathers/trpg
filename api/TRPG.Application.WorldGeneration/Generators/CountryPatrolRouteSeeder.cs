using TRPG.Application.Configuration;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public record CountryPatrolRouteSeederResult(
    IReadOnlyList<Route> Routes,
    IReadOnlyList<RouteStep> Steps,
    IReadOnlyList<RouteTraveler> Travelers,
    IReadOnlyList<RouteTravelerMember> Members,
    IReadOnlyList<Creature> Creatures,
    IReadOnlyList<FactionMember> FactionMembers,
    IReadOnlyList<Item> Items,
    IReadOnlyList<CreatureSkill> Skills
);

public class CountryPatrolRouteSeeder(CreatureGroupGenerator creatureGroupGenerator)
{
    public CountryPatrolRouteSeederResult Seed(
        WorldGeneratorResult world,
        CountryPatrolOptions options
    )
    {
        var routes = new List<Route>();
        var steps = new List<RouteStep>();
        var travelers = new List<RouteTraveler>();
        var members = new List<RouteTravelerMember>();
        var creatures = new List<Creature>();
        var factionMembers = new List<FactionMember>();
        var items = new List<Item>();
        var skills = new List<CreatureSkill>();
        var graph = TravelGraph.Build(world);
        var countryIdByLocationId = BuildCountryIdByLocationId(world);
        var districtsByCityId = world.Districts.ToLookup(district => district.CityId);

        foreach (var country in world.Countries)
        {
            var capital = world.Cities.FirstOrDefault(city =>
                city.IsCapital && city.CountryId == country.Id
            );
            var guardFaction = world.Factions.FirstOrDefault(faction =>
                faction.Kind == FactionKind.CityGuard && faction.CityId == capital?.Id
            );
            if (capital == null || guardFaction == null)
            {
                continue;
            }

            var entranceLocationIds = world
                .Cities.Where(city => city.CountryId == country.Id)
                .Select(city =>
                    districtsByCityId[city.Id]
                        .FirstOrDefault(district =>
                            district.DistrictType == DistrictType.CityEntrance
                        )
                )
                .Where(district => district != null)
                .Select(district => district!.LocationId)
                .ToArray();
            if (entranceLocationIds.Length < 2)
            {
                continue;
            }

            var countryGraph = FilterToCountry(graph, countryIdByLocationId, country.Id);
            var orderedEntrances = CaravanRouteSeeder.OrderByDfsPreorder(
                entranceLocationIds,
                countryGraph
            );
            if (orderedEntrances.Count < 2)
            {
                continue;
            }

            var route = new Route
            {
                WorldId = world.World.Id,
                Name = $"{country.Name} Road Patrol",
                Traversal = RouteTraversal.Cyclic,
            };
            var entranceSet = entranceLocationIds.ToHashSet();
            var routeSteps = TravelGraph
                .BuildCycle(countryGraph, orderedEntrances)
                .Select(
                    (leg, index) =>
                        new RouteStep
                        {
                            WorldId = world.World.Id,
                            RouteId = route.Id,
                            SequenceIndex = index,
                            LocationId = leg.OriginLocationId,
                            ConnectorId = leg.ConnectorId,
                            DwellHours = entranceSet.Contains(leg.OriginLocationId)
                                ? 0
                                : options.DefaultLingerHours,
                        }
                )
                .ToArray();
            if (routeSteps.Length == 0)
            {
                continue;
            }

            var traveler = new RouteTraveler
            {
                WorldId = world.World.Id,
                RouteId = route.Id,
                StartedAtGameTime = GameClock.Epoch,
                SpeedUnitsPerHour = options.SpeedUnitsPerHour,
                Purpose = $"Patrolling the roads of {country.Name}.",
            };
            var guardGroup = creatureGroupGenerator.Generate(
                new CreatureGroupGeneratorInput(
                    country.DominantRace,
                    Profession.Guard,
                    world.World.Id,
                    routeSteps[0].LocationId,
                    Count: options.SquadSize,
                    MinLevel: options.MinGuardLevel,
                    MaxLevel: options.MaxGuardLevel
                )
            );

            routes.Add(route);
            steps.AddRange(routeSteps);
            travelers.Add(traveler);
            members.AddRange(
                guardGroup.Select(guard => new RouteTravelerMember
                {
                    WorldId = world.World.Id,
                    RouteTravelerId = traveler.Id,
                    CreatureId = guard.Creature.Id,
                })
            );
            creatures.AddRange(guardGroup.Select(guard => guard.Creature));
            factionMembers.AddRange(
                guardGroup.Select(guard => new FactionMember
                {
                    WorldId = world.World.Id,
                    FactionId = guardFaction.Id,
                    CreatureId = guard.Creature.Id,
                    Role = FactionRole.Member,
                })
            );
            items.AddRange(guardGroup.SelectMany(guard => guard.Items));
            skills.AddRange(guardGroup.SelectMany(guard => guard.Skills));
        }

        return new CountryPatrolRouteSeederResult(
            routes,
            steps,
            travelers,
            members,
            creatures,
            factionMembers,
            items,
            skills
        );
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
            .Where(entry => countryIdByLocationId.GetValueOrDefault(entry.Key) == countryId)
            .ToDictionary(
                entry => entry.Key,
                entry =>
                    (IReadOnlyList<TravelGraphEdge>)
                        entry
                            .Value.Where(edge =>
                                countryIdByLocationId.GetValueOrDefault(edge.DestinationLocationId)
                                == countryId
                            )
                            .ToArray()
            );
}
