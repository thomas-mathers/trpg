using TRPG.Application.Common.Navigation;
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
    public static CaravanRouteSeederResult Seed(
        WorldGeneratorResult world,
        CaravanOptions options,
        double timeScale
    )
    {
        var capitalLocationIds = ResolveCapitalEntranceLocationIds(world);
        if (capitalLocationIds.Count < 2)
        {
            return new CaravanRouteSeederResult([], [], [], [], []);
        }

        var graph = world.BuildTravelGraph();
        var metersPerHour = InLocationPace.MetersPerGameHour(options.SpeedUnitsPerHour, timeScale);
        var clockwiseLocations = OrderByDfsPreorder(capitalLocationIds, graph);
        var counterClockwiseLocations = clockwiseLocations
            .Take(1)
            .Concat(clockwiseLocations.Skip(1).Reverse())
            .ToArray();
        var clockwise = BuildRoute(
            world,
            options,
            graph,
            metersPerHour,
            clockwiseLocations,
            "The Capital Circuit — Clockwise"
        );
        var counterClockwise = BuildRoute(
            world,
            options,
            graph,
            metersPerHour,
            counterClockwiseLocations,
            "The Capital Circuit — Counter-clockwise"
        );

        var layoutContext = new CreatureLayoutContext(
            new CreatureLayoutInput(
                world.Locations,
                world.Props,
                world.Buildings,
                PlacedConnector.Place(world.LocationConnectors, world.TravelNodes),
                []
            )
        );
        var signs = capitalLocationIds
            .Select(locationId => CreateSign(world, layoutContext, locationId))
            .ToArray();

        return new CaravanRouteSeederResult(
            [clockwise.Route, counterClockwise.Route],
            clockwise.Steps.Concat(counterClockwise.Steps).ToArray(),
            clockwise.Travelers.Concat(counterClockwise.Travelers).ToArray(),
            [clockwise.Fare, counterClockwise.Fare],
            signs
        );
    }

    private static CaravanScheduleSign CreateSign(
        WorldGeneratorResult world,
        CreatureLayoutContext layoutContext,
        Guid locationId
    )
    {
        var sign = new CaravanScheduleSign
        {
            WorldId = world.World.Id,
            LocationId = locationId,
            Name = "Caravan Schedule",
            Description = "A wooden signpost listing caravan arrival times.",
        };
        var footprint = PropFootprintCatalog.Get(PropModelResolver.Resolve(sign)).Footprint;
        var location = layoutContext.LocationById[locationId];
        var preferred = new Placement(location.Width * 2 / 3, location.Depth / 2, 0);
        var placement = CreaturePlacementResolver.PlaceAt(
            new Footprint(Width: location.Width, Depth: location.Depth),
            layoutContext.ObstaclesAt(locationId, excludedPropId: null),
            preferred
        );

        sign.X = placement.X;
        sign.Y = placement.Y;
        sign.Angle = placement.Angle;
        sign.Width = footprint.Width;
        sign.Depth = footprint.Depth;

        return sign;
    }

    private static SeededCaravanRoute BuildRoute(
        WorldGeneratorResult world,
        CaravanOptions options,
        TravelGraph graph,
        double metersPerHour,
        IReadOnlyList<Guid> orderedStopLocationIds,
        string name
    )
    {
        var route = new Route { WorldId = world.World.Id, Name = name };
        var stopLocationIds = orderedStopLocationIds.ToHashSet();
        var legs = graph.BuildCycle(orderedStopLocationIds);
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
                        Distance = leg.Distance,
                    }
            )
            .ToArray();
        var durationHours = steps
            .Select((step, index) => step.DwellHours + legs[index].Distance / metersPerHour)
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
                SpeedUnitsPerHour = metersPerHour,
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
        TravelGraph graph
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

            foreach (var connector in graph.ConnectorsFrom(current))
            {
                if (!visited.Contains(connector.DestinationLocationId))
                {
                    stack.Push(connector.DestinationLocationId);
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
