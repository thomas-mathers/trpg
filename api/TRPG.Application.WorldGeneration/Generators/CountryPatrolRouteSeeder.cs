using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public record CountryPatrolRouteSeederResult(
    IReadOnlyList<Route> Routes,
    IReadOnlyList<RouteStop> Stops,
    IReadOnlyList<RouteTraveler> Travelers,
    IReadOnlyList<RouteTravelerMember> Members,
    IReadOnlyList<Creature> Creatures,
    IReadOnlyList<FactionMember> FactionMembers,
    IReadOnlyList<Item> Items,
    IReadOnlyList<CreatureSkill> Skills
);

// A country's own cities never connect to each other directly — every trip already goes
// cityEntrance -> own state's shared wilderness hub -> (state-hub tree edges) -> destination
// state's hub -> cityEntrance. The world's full state-hub graph is NOT a pure tree, though: the
// global MST that builds it only guarantees the whole world is connected, so MapGenerator layers
// extra same-country "bridge" roads on top wherever the base MST would otherwise force a
// same-country trip through another country's territory (see MapGenerator.ConnectSameCountryComponents).
// Those bridges create cycles, so a same-country pair can have more than one path between them, and
// an unfiltered shortest-path/DFS walk over the whole graph is not guaranteed to prefer the
// same-country one. This seeder therefore restricts the adjacency graph to same-country edges only
// before pathfinding, rather than relying on graph shape to keep patrols from crossing a border.
//
// The patrol lingers at the wilderness waypoints between cities, never at a city's own gate — city
// entrances only decide which pairs of cities' connecting road gets walked and in what order; a
// city's own guards already cover its gate (BarracksGuardDutyAssigner), so a patrol stopping there
// too would just double up on the same spot. A country's cities sit at the leaves of what is mostly
// a tree-shaped road network, so a loop visiting every one of them generally has to double back over
// some of the same wilderness road at least once — the same wilderness stop can legitimately appear
// more than once in a route, and that is an accurate reflection of the road network, not a bug.
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
        var members = new List<RouteTravelerMember>();
        var creatures = new List<Creature>();
        var factionMembers = new List<FactionMember>();
        var items = new List<Item>();
        var skills = new List<CreatureSkill>();

        var adjacency = CaravanRouteSeeder.BuildTravelAdjacency(world);
        var countryIdByLocationId = BuildCountryIdByLocationId(world);
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

            var sameCountryAdjacency = FilterAdjacencyToCountry(
                adjacency,
                countryIdByLocationId,
                country.Id
            );

            var orderedEntranceLocationIds = CaravanRouteSeeder.OrderByDfsPreorder(
                entranceLocationIds,
                sameCountryAdjacency
            );
            if (orderedEntranceLocationIds.Count < 2)
            {
                continue;
            }

            var routeId = Guid.NewGuid();

            var routeStops = BuildWildernessStops(
                orderedEntranceLocationIds,
                sameCountryAdjacency,
                routeId
            );
            if (routeStops.Count == 0)
            {
                continue;
            }

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
                Kind = RouteTravelerKind.GuardPatrol,
                Purpose = $"Patrolling the roads of {country.Name}.",
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
            stops,
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

    // Walks the full loop leg by leg (each leg being the shortest path between one pair of
    // consecutive cities), then drops the city-entrance nodes from the result — folding the
    // distance of whatever edges led into and out of a dropped entrance into a single distance
    // between the wilderness stops on either side of it.
    private static IReadOnlyList<RouteStop> BuildWildernessStops(
        IReadOnlyList<Guid> orderedEntranceLocationIds,
        IReadOnlyDictionary<Guid, List<(Guid Neighbor, float Distance)>> adjacency,
        Guid routeId
    )
    {
        var entranceLocationIds = orderedEntranceLocationIds.ToHashSet();

        var fullCycle = new List<(Guid LocationId, float DistanceFromPrevious)>();
        for (var i = 0; i < orderedEntranceLocationIds.Count; i++)
        {
            var from = orderedEntranceLocationIds[i];
            var to = orderedEntranceLocationIds[(i + 1) % orderedEntranceLocationIds.Count];
            var legPath = ShortestPath(adjacency, from, to);
            fullCycle.AddRange(i == 0 ? legPath : legPath.Skip(1));
        }

        // The walk above starts and ends at the same location (the final leg wraps back to the
        // first entrance) — fold that closing distance into the first node's own incoming distance
        // so this becomes a clean cycle instead of a line with a duplicated endpoint.
        var wrapDistance = fullCycle[^1].DistanceFromPrevious;
        fullCycle.RemoveAt(fullCycle.Count - 1);
        fullCycle[0] = (fullCycle[0].LocationId, wrapDistance);

        var wildernessIndices = Enumerable
            .Range(0, fullCycle.Count)
            .Where(index => !entranceLocationIds.Contains(fullCycle[index].LocationId))
            .ToArray();
        if (wildernessIndices.Length == 0)
        {
            return [];
        }

        var stops = new List<RouteStop>();
        for (var sequenceIndex = 0; sequenceIndex < wildernessIndices.Length; sequenceIndex++)
        {
            var index = wildernessIndices[sequenceIndex];
            var nextIndex = wildernessIndices[(sequenceIndex + 1) % wildernessIndices.Length];

            var distanceToNextStop = 0f;
            for (var j = (index + 1) % fullCycle.Count; ; j = (j + 1) % fullCycle.Count)
            {
                distanceToNextStop += fullCycle[j].DistanceFromPrevious;
                if (j == nextIndex)
                {
                    break;
                }
            }

            stops.Add(
                new RouteStop
                {
                    RouteId = routeId,
                    SequenceIndex = sequenceIndex,
                    LocationId = fullCycle[index].LocationId,
                    DistanceToNextStop = distanceToNextStop,
                }
            );
        }

        return stops;
    }

    private static IReadOnlyList<(Guid LocationId, float DistanceFromPrevious)> ShortestPath(
        IReadOnlyDictionary<Guid, List<(Guid Neighbor, float Distance)>> adjacency,
        Guid from,
        Guid to
    )
    {
        var cameFrom = new Dictionary<Guid, (Guid Parent, float Distance)> { [from] = (from, 0) };
        var queue = new Queue<Guid>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == to)
            {
                break;
            }

            foreach (var (neighbor, distance) in adjacency.GetValueOrDefault(current, []))
            {
                if (cameFrom.ContainsKey(neighbor))
                {
                    continue;
                }

                cameFrom[neighbor] = (current, distance);
                queue.Enqueue(neighbor);
            }
        }

        var path = new List<(Guid, float)>();
        var node = to;
        while (node != from)
        {
            var (parent, distance) = cameFrom[node];
            path.Add((node, distance));
            node = parent;
        }
        path.Add((from, 0));
        path.Reverse();

        return path;
    }

    private static Dictionary<Guid, List<(Guid Neighbor, float Distance)>> FilterAdjacencyToCountry(
        IReadOnlyDictionary<Guid, List<(Guid Neighbor, float Distance)>> adjacency,
        IReadOnlyDictionary<Guid, Guid> countryIdByLocationId,
        Guid countryId
    )
    {
        var filtered = new Dictionary<Guid, List<(Guid Neighbor, float Distance)>>();

        foreach (var (locationId, neighbors) in adjacency)
        {
            if (
                !countryIdByLocationId.TryGetValue(locationId, out var locationCountryId)
                || locationCountryId != countryId
            )
            {
                continue;
            }

            filtered[locationId] = neighbors
                .Where(neighbor =>
                    countryIdByLocationId.TryGetValue(neighbor.Neighbor, out var neighborCountryId)
                    && neighborCountryId == countryId
                )
                .ToList();
        }

        return filtered;
    }
}
