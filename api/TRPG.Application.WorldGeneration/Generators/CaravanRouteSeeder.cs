using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public record CaravanRouteSeederResult(
    CaravanRoute? Route,
    IReadOnlyList<CaravanRouteStop> Stops,
    IReadOnlyList<Caravan> Caravans,
    IReadOnlyList<CaravanScheduleSign> Signs
);

// Cities never connect to each other directly — every trip already goes
// cityEntrance -> own state's shared wilderness hub -> (state-hub tree edges) -> destination
// state's hub -> cityEntrance, and the state-hub graph is a minimum spanning tree (fully
// connected, no cycles). So a capital-to-capital loop has no natural cycle to find in the graph;
// the loop is a designed visiting order, and each leg's distance is just the unique tree-path sum
// between two capitals.
public static class CaravanRouteSeeder
{
    public static CaravanRouteSeederResult Seed(WorldGeneratorResult world, CaravanOptions options)
    {
        var capitalLocationIds = ResolveCapitalEntranceLocationIds(world);
        if (capitalLocationIds.Count < 2)
        {
            return new CaravanRouteSeederResult(null, [], [], []);
        }

        var adjacency = BuildTravelAdjacency(world);
        var orderedStops = OrderByDfsPreorder(capitalLocationIds, adjacency);
        var routeId = Guid.NewGuid();

        var stops = orderedStops
            .Select(
                (locationId, index) =>
                    new CaravanRouteStop
                    {
                        CaravanRouteId = routeId,
                        SequenceIndex = index,
                        LocationId = locationId,
                        DistanceToNextStop = PathDistance(
                            adjacency,
                            locationId,
                            orderedStops[(index + 1) % orderedStops.Count]
                        ),
                    }
            )
            .ToArray();

        var totalCycleHours = stops.Sum(stop =>
            options.DefaultLingerHours
            + LegHours(stop.DistanceToNextStop, options.SpeedUnitsPerHour)
        );

        var route = new CaravanRoute
        {
            Id = routeId,
            WorldId = world.World.Id,
            Name = "The Capital Circuit",
            TicketFeeGold = options.DefaultTicketFeeGold,
            LingerHours = options.DefaultLingerHours,
        };

        // Traveling the loop backward covers the same set of legs in a different order, so the
        // total cycle length is identical for both directions — each direction's instances are
        // spread independently over that same span.
        var instancesPerDirection = stops.Length * options.CaravansPerStop;
        var caravans = new[] { CaravanDirection.Clockwise, CaravanDirection.CounterClockwise }
            .SelectMany(direction =>
                Enumerable
                    .Range(0, instancesPerDirection)
                    .Select(i => new Caravan
                    {
                        WorldId = world.World.Id,
                        CaravanRouteId = route.Id,
                        Direction = direction,
                        PhaseOffsetHours = (double)i * totalCycleHours / instancesPerDirection,
                    })
            )
            .ToArray();

        // The sign's Description is a static placeholder only — its actual displayed text is
        // computed live from current caravan positions by the /signs/{signId} endpoint, so it
        // never goes stale the way baking a schedule in at world creation would.
        var signs = stops
            .Select(stop => new CaravanScheduleSign
            {
                WorldId = world.World.Id,
                LocationId = stop.LocationId,
                Name = "Caravan Schedule",
                Description = "A wooden signpost listing caravan arrival times.",
            })
            .ToArray();

        return new CaravanRouteSeederResult(route, stops, caravans, signs);
    }

    private static int LegHours(float distance, float speedUnitsPerHour) =>
        Math.Max(1, (int)(distance / speedUnitsPerHour));

    private static IReadOnlyList<Guid> ResolveCapitalEntranceLocationIds(WorldGeneratorResult world)
    {
        var districtsByCityId = world.Districts.ToLookup(d => d.CityId);

        return world
            .Countries.Select(country =>
                world.Cities.FirstOrDefault(c => c.IsCapital && c.CountryId == country.Id)
            )
            .Where(capital => capital != null)
            .Select(capital =>
                districtsByCityId[capital!.Id]
                    .FirstOrDefault(d => d.DistrictType == DistrictType.CityEntrance)
            )
            .Where(entrance => entrance != null)
            .Select(entrance => entrance!.LocationId)
            .ToArray();
    }

    private static Dictionary<Guid, List<(Guid Neighbor, float Distance)>> BuildTravelAdjacency(
        WorldGeneratorResult world
    )
    {
        var distanceByConnectorId = world.TravelConnectors.ToDictionary(
            t => t.ConnectorId,
            t => t.Distance
        );
        var adjacency = new Dictionary<Guid, List<(Guid Neighbor, float Distance)>>();

        foreach (var connector in world.LocationConnectors)
        {
            if (!distanceByConnectorId.TryGetValue(connector.Id, out var distance))
            {
                continue;
            }

            if (!adjacency.TryGetValue(connector.OriginLocationId, out var neighbors))
            {
                neighbors = [];
                adjacency[connector.OriginLocationId] = neighbors;
            }

            neighbors.Add((connector.DestinationLocationId, distance));
        }

        return adjacency;
    }

    private static IReadOnlyList<Guid> OrderByDfsPreorder(
        IReadOnlyCollection<Guid> capitalLocationIds,
        IReadOnlyDictionary<Guid, List<(Guid Neighbor, float Distance)>> adjacency
    )
    {
        var remaining = new HashSet<Guid>(capitalLocationIds);
        var visited = new HashSet<Guid>();
        var order = new List<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(capitalLocationIds.First());

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (remaining.Contains(current))
            {
                order.Add(current);
            }

            foreach (var (neighbor, _) in adjacency.GetValueOrDefault(current, []))
            {
                if (!visited.Contains(neighbor))
                {
                    stack.Push(neighbor);
                }
            }
        }

        return order;
    }

    private static float PathDistance(
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

        var total = 0f;
        var node = to;
        while (node != from)
        {
            var (parent, distance) = cameFrom[node];
            total += distance;
            node = parent;
        }

        return total;
    }
}
