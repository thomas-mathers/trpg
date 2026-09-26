using TRPG.Application.Configuration;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public record CaravanRouteSeederResult(
    IReadOnlyList<Route> Routes,
    IReadOnlyList<RouteStep> Steps,
    IReadOnlyList<RouteTraveler> Travelers,
    IReadOnlyList<CaravanFare> Fares,
    IReadOnlyList<CaravanScheduleSign> Signs
);

public static class CaravanRouteSeeder
{
    public static CaravanRouteSeederResult Seed(WorldGeneratorResult world, CaravanOptions options)
    {
        var capitalLocationIds = ResolveCapitalEntranceLocationIds(world);
        if (capitalLocationIds.Count < 2)
        {
            return new CaravanRouteSeederResult([], [], [], [], []);
        }

        var graph = TravelGraph.Build(world);
        var clockwiseLocations = OrderByDfsPreorder(capitalLocationIds, graph);
        var counterClockwiseLocations = clockwiseLocations
            .Take(1)
            .Concat(clockwiseLocations.Skip(1).Reverse())
            .ToArray();
        var clockwise = BuildRoute(
            world,
            options,
            graph,
            clockwiseLocations,
            "The Capital Circuit — Clockwise"
        );
        var counterClockwise = BuildRoute(
            world,
            options,
            graph,
            counterClockwiseLocations,
            "The Capital Circuit — Counter-clockwise"
        );

        var signs = capitalLocationIds
            .Select(locationId => new CaravanScheduleSign
            {
                WorldId = world.World.Id,
                LocationId = locationId,
                Name = "Caravan Schedule",
                Description = "A wooden signpost listing caravan arrival times.",
            })
            .ToArray();

        return new CaravanRouteSeederResult(
            [clockwise.Route, counterClockwise.Route],
            clockwise.Steps.Concat(counterClockwise.Steps).ToArray(),
            clockwise.Travelers.Concat(counterClockwise.Travelers).ToArray(),
            [clockwise.Fare, counterClockwise.Fare],
            signs
        );
    }

    private static SeededCaravanRoute BuildRoute(
        WorldGeneratorResult world,
        CaravanOptions options,
        IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> graph,
        IReadOnlyList<Guid> orderedStopLocationIds,
        string name
    )
    {
        var route = new Route
        {
            WorldId = world.World.Id,
            Name = name,
            Traversal = RouteTraversal.Cyclic,
        };
        var stopLocationIds = orderedStopLocationIds.ToHashSet();
        var legs = TravelGraph.BuildCycle(graph, orderedStopLocationIds);
        var steps = legs.Select(
                (leg, index) =>
                    new RouteStep
                    {
                        WorldId = world.World.Id,
                        RouteId = route.Id,
                        SequenceIndex = index,
                        LocationId = leg.OriginLocationId,
                        ConnectorId = leg.ConnectorId,
                        DwellHours = stopLocationIds.Contains(leg.OriginLocationId)
                            ? options.DefaultLingerHours
                            : 0,
                    }
            )
            .ToArray();
        var durationHours = steps
            .Select(
                (step, index) => step.DwellHours + legs[index].Distance / options.SpeedUnitsPerHour
            )
            .Sum();
        var instanceCount = orderedStopLocationIds.Count * options.CaravansPerStop;
        var travelers = Enumerable
            .Range(0, instanceCount)
            .Select(index => new RouteTraveler
            {
                WorldId = world.World.Id,
                RouteId = route.Id,
                StartedAtGameTime =
                    GameClock.Epoch - TimeSpan.FromHours(1) * durationHours * index / instanceCount,
                SpeedUnitsPerHour = options.SpeedUnitsPerHour,
                Purpose = name,
            })
            .ToArray();
        var fare = new CaravanFare
        {
            WorldId = world.World.Id,
            RouteId = route.Id,
            TicketFeeGold = options.DefaultTicketFeeGold,
        };
        return new SeededCaravanRoute(route, steps, travelers, fare);
    }

    private static IReadOnlyList<Guid> ResolveCapitalEntranceLocationIds(WorldGeneratorResult world)
    {
        var districtsByCityId = world.Districts.ToLookup(district => district.CityId);
        return world
            .Countries.Select(country =>
                world.Cities.FirstOrDefault(city => city.IsCapital && city.CountryId == country.Id)
            )
            .Where(city => city != null)
            .Select(city =>
                districtsByCityId[city!.Id]
                    .FirstOrDefault(district => district.DistrictType == DistrictType.CityEntrance)
            )
            .Where(district => district != null)
            .Select(district => district!.LocationId)
            .ToArray();
    }

    internal static IReadOnlyList<Guid> OrderByDfsPreorder(
        IReadOnlyCollection<Guid> stopLocationIds,
        IReadOnlyDictionary<Guid, IReadOnlyList<TravelGraphEdge>> graph
    )
    {
        var remaining = stopLocationIds.ToHashSet();
        var visited = new HashSet<Guid>();
        var order = new List<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(stopLocationIds.First());

        while (stack.TryPop(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            if (remaining.Contains(current))
            {
                order.Add(current);
            }

            foreach (var edge in graph.GetValueOrDefault(current, []))
            {
                if (!visited.Contains(edge.DestinationLocationId))
                {
                    stack.Push(edge.DestinationLocationId);
                }
            }
        }

        return order.ToArray();
    }

    private record SeededCaravanRoute(
        Route Route,
        IReadOnlyList<RouteStep> Steps,
        IReadOnlyList<RouteTraveler> Travelers,
        CaravanFare Fare
    );
}
