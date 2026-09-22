using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public record CountryPatrolRouteSeederResult(
    IReadOnlyList<Route> Routes,
    IReadOnlyList<RouteStop> Stops,
    IReadOnlyList<RouteTraveler> Travelers,
    IReadOnlyList<GuardPatrolMember> Members,
    IReadOnlyList<Creature> Creatures,
    IReadOnlyList<FactionMember> FactionMembers,
    IReadOnlyList<Item> Items,
    IReadOnlyList<CreatureSkill> Skills
);

// A country's own cities never connect to each other directly — every trip already goes
// cityEntrance -> own state's shared wilderness hub -> (state-hub tree edges) -> destination
// state's hub -> cityEntrance, and the world's full state-hub graph is a single minimum spanning
// tree across every country. A country's states form a connected subtree of that larger tree (the
// same-country road-reachability guarantee world generation enforces elsewhere), and a tree has
// exactly one simple path between any two nodes — so the unique path between two of this
// country's own cities can never pass through a node outside that subtree, i.e. it never crosses
// into another country's territory, without needing to filter the adjacency graph by country here.
public class CountryPatrolRouteSeeder(CreatureGroupGenerator creatureGroupGenerator)
{
    public CountryPatrolRouteSeederResult Seed(
        WorldGeneratorResult world,
        CountryPatrolOptions options
    )
    {
        var routes = new List<Route>();
        var stops = new List<RouteStop>();
        var travelers = new List<RouteTraveler>();
        var members = new List<GuardPatrolMember>();
        var creatures = new List<Creature>();
        var factionMembers = new List<FactionMember>();
        var items = new List<Item>();
        var skills = new List<CreatureSkill>();

        var adjacency = CaravanRouteSeeder.BuildTravelAdjacency(world);
        var districtsByCityId = world.Districts.ToLookup(d => d.CityId);

        foreach (var country in world.Countries)
        {
            var capital = world.Cities.FirstOrDefault(c =>
                c.IsCapital && c.CountryId == country.Id
            );
            if (capital == null)
            {
                continue;
            }

            var guardFaction = world.Factions.FirstOrDefault(f =>
                f.Kind == FactionKind.CityGuard && f.CityId == capital.Id
            );
            if (guardFaction == null)
            {
                continue;
            }

            var entranceLocationIds = world
                .Cities.Where(c => c.CountryId == country.Id)
                .Select(c =>
                    districtsByCityId[c.Id]
                        .FirstOrDefault(d => d.DistrictType == DistrictType.CityEntrance)
                )
                .Where(entrance => entrance != null)
                .Select(entrance => entrance!.LocationId)
                .ToArray();
            if (entranceLocationIds.Length < 2)
            {
                continue;
            }

            var orderedStops = CaravanRouteSeeder.OrderByDfsPreorder(
                entranceLocationIds,
                adjacency
            );
            var routeId = Guid.NewGuid();

            var routeStops = orderedStops
                .Select(
                    (locationId, index) =>
                        new RouteStop
                        {
                            RouteId = routeId,
                            SequenceIndex = index,
                            LocationId = locationId,
                            DistanceToNextStop = CaravanRouteSeeder.PathDistance(
                                adjacency,
                                locationId,
                                orderedStops[(index + 1) % orderedStops.Count]
                            ),
                        }
                )
                .ToArray();

            var route = new Route
            {
                Id = routeId,
                WorldId = world.World.Id,
                Name = $"{country.Name} Road Patrol",
                LingerHours = options.DefaultLingerHours,
            };

            var traveler = new RouteTraveler
            {
                WorldId = world.World.Id,
                RouteId = routeId,
                Direction = RouteDirection.Clockwise,
                PhaseOffsetHours = 0,
            };

            var guardGroup = creatureGroupGenerator.Generate(
                new CreatureGroupGeneratorInput(
                    country.DominantRace,
                    Profession.Guard,
                    world.World.Id,
                    routeStops[0].LocationId,
                    Count: options.SquadSize,
                    MinLevel: options.MinGuardLevel,
                    MaxLevel: options.MaxGuardLevel
                )
            );

            routes.Add(route);
            stops.AddRange(routeStops);
            travelers.Add(traveler);
            members.AddRange(
                guardGroup.Select(guard => new GuardPatrolMember
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
            stops,
            travelers,
            members,
            creatures,
            factionMembers,
            items,
            skills
        );
    }
}
