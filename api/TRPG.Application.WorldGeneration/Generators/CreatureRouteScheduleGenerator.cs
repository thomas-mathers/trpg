using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public record CreatureRouteScheduleGeneratorResult(
    IReadOnlyList<Route> Routes,
    IReadOnlyList<RouteStep> Steps,
    IReadOnlyList<CreatureRouteSchedule> Schedules
);

public static class CreatureRouteScheduleGenerator
{
    private const int HoursPerDay = 24;
    private const int HoursPerWeek = 7 * HoursPerDay;
    private const double LunchJitterHours = 1.0 / 3;
    private const double EndOfShiftJitterHours = 0.5;

    public static CreatureRouteScheduleGeneratorResult Generate(
        Guid worldId,
        IReadOnlyCollection<Creature> creatures,
        IReadOnlyCollection<CreatureJob> jobs,
        IReadOnlyCollection<LocationConnector> locationConnectors,
        IReadOnlyCollection<TravelConnector> travelConnectors
    )
    {
        var graph = TravelGraph.Build(locationConnectors, travelConnectors);
        var creaturesById = creatures.ToDictionary(creature => creature.Id);
        var routes = new List<Route>();
        var steps = new List<RouteStep>();
        var schedules = new List<CreatureRouteSchedule>();
        var routesByPath = new Dictionary<string, Route>();
        var state = new GenerationState
        {
            WorldId = worldId,
            Graph = graph,
            Routes = routes,
            Steps = steps,
            Schedules = schedules,
            RoutesByPath = routesByPath,
        };

        foreach (var group in jobs.GroupBy(job => job.CreatureId))
        {
            if (
                !creaturesById.TryGetValue(group.Key, out var creature)
                || creature.MovementSpeed <= 0
            )
            {
                continue;
            }

            GenerateForCreature(creature, group.ToArray(), state);
        }

        return new CreatureRouteScheduleGeneratorResult(routes, steps, schedules);
    }

    private static void GenerateForCreature(
        Creature creature,
        IReadOnlyCollection<CreatureJob> jobs,
        GenerationState state
    )
    {
        for (var weekHour = 0; weekHour < HoursPerWeek; weekHour++)
        {
            var destination = FindDueJob(jobs, weekHour);
            var origin = FindDueJob(jobs, NormalizeWeekHour(weekHour - 1));
            if (
                destination == null
                || origin == null
                || destination.Id == origin.Id
                || destination.LocationId == origin.LocationId
            )
            {
                continue;
            }

            var path = TravelGraph.FindShortestPath(
                state.Graph,
                origin.LocationId,
                destination.LocationId
            );
            if (path.Count == 0)
            {
                continue;
            }

            var durationHours = path.Sum(leg => leg.Distance) / creature.MovementSpeed;
            var departureWeekHour = NormalizeWeekHour(
                ResolveDepartureWeekHour(creature.Id, origin, destination, weekHour, durationHours)
            );
            var route = GetOrCreateRoute(path, state);
            state.Schedules.Add(
                new CreatureRouteSchedule
                {
                    WorldId = state.WorldId,
                    CreatureId = creature.Id,
                    RouteId = route.Id,
                    OriginCreatureJobId = origin.Id,
                    DestinationCreatureJobId = destination.Id,
                    DepartureDay = (DayOfWeek)(int)(departureWeekHour / HoursPerDay),
                    DepartureHour = departureWeekHour % HoursPerDay,
                    DurationHours = durationHours,
                    Purpose = PurposeFor(destination.Action),
                }
            );
        }
    }

    private static double ResolveDepartureWeekHour(
        Guid creatureId,
        CreatureJob origin,
        CreatureJob destination,
        int transitionWeekHour,
        double durationHours
    )
    {
        var jitter = StableUnitInterval(creatureId, origin.Id, destination.Id, transitionWeekHour);
        if (destination.Action == CreatureJobAction.Eat)
        {
            return transitionWeekHour - durationHours + (jitter * 2 - 1) * LunchJitterHours;
        }
        if (origin.Action == CreatureJobAction.Work)
        {
            return transitionWeekHour + jitter * EndOfShiftJitterHours;
        }

        return transitionWeekHour - durationHours;
    }

    private static double StableUnitInterval(
        Guid creatureId,
        Guid originJobId,
        Guid destinationJobId,
        int transitionWeekHour
    )
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        var hash = offset;
        foreach (
            var value in creatureId
                .ToByteArray()
                .Concat(originJobId.ToByteArray())
                .Concat(destinationJobId.ToByteArray())
                .Concat(BitConverter.GetBytes(transitionWeekHour))
        )
        {
            hash = unchecked((hash ^ value) * prime);
        }

        return (hash >> 11) * (1.0 / (1UL << 53));
    }

    private static CreatureJob? FindDueJob(IReadOnlyCollection<CreatureJob> jobs, double weekHour)
    {
        var day = (DayOfWeek)(int)(weekHour / HoursPerDay);
        var hour = (int)(weekHour % HoursPerDay);
        return jobs.Where(job => IsActive(job, day, hour))
            .OrderByDescending(job => job.Priority)
            .ThenBy(job => job.Id)
            .FirstOrDefault();
    }

    private static bool IsActive(CreatureJob job, DayOfWeek day, int hour)
    {
        if (job.SpecificDay != null && job.SpecificDay != day)
        {
            return false;
        }

        return job.StartHour <= job.EndHour
            ? hour >= job.StartHour && hour < job.EndHour
            : hour >= job.StartHour || hour < job.EndHour;
    }

    private static Route GetOrCreateRoute(IReadOnlyList<TravelPathLeg> path, GenerationState state)
    {
        var key = string.Join(',', path.Select(leg => leg.ConnectorId));
        if (state.RoutesByPath.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var route = new Route
        {
            WorldId = state.WorldId,
            Name = $"{path[0].OriginLocationId:N} to {path[^1].DestinationLocationId:N}",
            Traversal = RouteTraversal.Finite,
        };
        state.Routes.Add(route);
        state.Steps.AddRange(
            path.Select(
                (leg, index) =>
                    new RouteStep
                    {
                        WorldId = state.WorldId,
                        RouteId = route.Id,
                        SequenceIndex = index,
                        LocationId = leg.OriginLocationId,
                        ConnectorId = leg.ConnectorId,
                        DwellHours = 0,
                    }
            )
        );
        state.Steps.Add(
            new RouteStep
            {
                WorldId = state.WorldId,
                RouteId = route.Id,
                SequenceIndex = path.Count,
                LocationId = path[^1].DestinationLocationId,
                ConnectorId = null,
                DwellHours = 0,
            }
        );
        state.RoutesByPath[key] = route;
        return route;
    }

    private static double NormalizeWeekHour(double hour)
    {
        var normalized = hour % HoursPerWeek;
        return normalized < 0 ? normalized + HoursPerWeek : normalized;
    }

    private static string PurposeFor(CreatureJobAction action) =>
        action switch
        {
            CreatureJobAction.Sleep => "Walking home to sleep",
            CreatureJobAction.Work => "Walking to work",
            CreatureJobAction.Idle => "Walking to spend free time",
            CreatureJobAction.Study => "Walking to study",
            CreatureJobAction.Pray => "Walking to pray",
            CreatureJobAction.Eat => "Walking home to eat",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };

    private sealed class GenerationState
    {
        public required Guid WorldId { get; init; }
        public required IReadOnlyDictionary<
            Guid,
            IReadOnlyList<TravelGraphEdge>
        > Graph { get; init; }
        public required List<Route> Routes { get; init; }
        public required List<RouteStep> Steps { get; init; }
        public required List<CreatureRouteSchedule> Schedules { get; init; }
        public required Dictionary<string, Route> RoutesByPath { get; init; }
    }
}
