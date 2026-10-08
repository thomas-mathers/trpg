using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record BarracksGuardDutyAssignerInput(
    Guid WorldId,
    string CityName,
    Guid CityFactionId,
    Guid GroundFloorLocationId,
    Guid GateLocationId,
    IReadOnlyList<District> Districts,
    IReadOnlyList<LocationConnector> DistrictConnectors,
    IReadOnlyList<Bed> Beds,
    IReadOnlyList<Creature> Guards
);

internal record BarracksGuardDutyAssignerResult(
    IReadOnlyList<FactionMember> FactionMembers,
    IReadOnlyList<CreatureJob> Jobs,
    IReadOnlyList<Route> Routes,
    IReadOnlyList<RouteStep> RouteSteps
);

// Guards have no household, so unlike shop staff they get no day-off activities.
internal static class BarracksGuardDutyAssigner
{
    private static readonly HourWindow DayShiftHours = new(6, 18);
    private static readonly HourWindow NightShiftHours = new(18, 6);
    private static readonly HourWindow DayShiftSleepHours = new(22, 6);
    private static readonly HourWindow NightShiftSleepHours = new(6, 14);

    internal static BarracksGuardDutyAssignerResult Generate(BarracksGuardDutyAssignerInput input)
    {
        var factionMembers = new List<FactionMember>();
        var jobs = new List<CreatureJob>();
        var routes = new List<Route>();
        var routeSteps = new List<RouteStep>();
        var patrolStops = PatrolDistrictOrder.Build(
            [.. input.Districts.Select(district => district.LocationId)],
            input.DistrictConnectors
        );

        for (var i = 0; i < input.Guards.Count; i++)
        {
            var guard = input.Guards[i];
            guard.LocationId = input.GroundFloorLocationId;

            factionMembers.Add(
                new FactionMember
                {
                    FactionId = input.CityFactionId,
                    CreatureId = guard.Id,
                    Role = FactionRole.Member,
                    WorldId = input.WorldId,
                }
            );

            var bedLocationId = input.Beds.First(b => b.AssignedCreatureId == guard.Id).LocationId;

            if (i is 1 or 2)
            {
                jobs.AddRange(AssignGate(input, guard.Id, bedLocationId, isDayShift: i == 1));
                continue;
            }

            var patrol = AssignPatrol(
                input,
                guard.Id,
                bedLocationId,
                patrolStops,
                StartStopIndex(i, input.Guards.Count, patrolStops.Count),
                isDayShift: IsDayPatrol(i)
            );
            jobs.AddRange(patrol.Jobs);
            if (patrol.Route != null)
            {
                routes.Add(patrol.Route);
                routeSteps.AddRange(patrol.Steps);
            }
        }

        return new BarracksGuardDutyAssignerResult(factionMembers, jobs, routes, routeSteps);
    }

    private static IReadOnlyList<CreatureJob> AssignGate(
        BarracksGuardDutyAssignerInput input,
        Guid guardId,
        Guid bedLocationId,
        bool isDayShift
    )
    {
        var shiftHours = isDayShift ? DayShiftHours : NightShiftHours;
        var sleepHours = isDayShift ? DayShiftSleepHours : NightShiftSleepHours;

        return
        [
            CreatureJobGenerator.GenerateSleep(guardId, bedLocationId, input.WorldId, sleepHours),
            CreatureJobGenerator.GenerateWork(
                guardId,
                input.GateLocationId,
                input.WorldId,
                shiftHours
            ),
        ];
    }

    private static GuardPatrolAssignment AssignPatrol(
        BarracksGuardDutyAssignerInput input,
        Guid guardId,
        Guid bedLocationId,
        IReadOnlyList<Guid> patrolStops,
        int startStopIndex,
        bool isDayShift
    )
    {
        var shiftHours = isDayShift ? DayShiftHours : NightShiftHours;
        var sleepHours = isDayShift ? DayShiftSleepHours : NightShiftSleepHours;

        var patrol = GeneratePatrolRoute(input, patrolStops, startStopIndex);
        if (patrol == null)
        {
            return new GuardPatrolAssignment(
                [
                    CreatureJobGenerator.GenerateSleep(
                        guardId,
                        bedLocationId,
                        input.WorldId,
                        sleepHours
                    ),
                    CreatureJobGenerator.GenerateWork(
                        guardId,
                        input.GroundFloorLocationId,
                        input.WorldId,
                        shiftHours
                    ),
                ],
                null,
                []
            );
        }

        return new GuardPatrolAssignment(
            [
                CreatureJobGenerator.GenerateSleep(
                    guardId,
                    bedLocationId,
                    input.WorldId,
                    sleepHours
                ),
                CreatureJobGenerator.GenerateWork(
                    guardId,
                    patrol.Steps[0].LocationId,
                    input.WorldId,
                    shiftHours,
                    patrol.Route.Id
                ),
            ],
            patrol.Route,
            patrol.Steps
        );
    }

    private static bool IsDayPatrol(int guardIndex) => guardIndex is 0 or 3 or 4;

    private static int StartStopIndex(int guardIndex, int guardCount, int stopCount)
    {
        var sameShift = Enumerable
            .Range(0, guardCount)
            .Where(other => other is not (1 or 2) && IsDayPatrol(other) == IsDayPatrol(guardIndex))
            .ToList();

        return sameShift.IndexOf(guardIndex) * stopCount / sameShift.Count;
    }

    private static CityPatrolRoute? GeneratePatrolRoute(
        BarracksGuardDutyAssignerInput input,
        IReadOnlyList<Guid> patrolStops,
        int startStopIndex
    )
    {
        if (patrolStops.Count < 2)
        {
            return null;
        }

        var route = new Route { WorldId = input.WorldId, Name = $"{input.CityName} City Patrol" };
        var steps = patrolStops
            .Skip(startStopIndex)
            .Concat(patrolStops.Take(startStopIndex))
            .Select(
                (locationId, index) =>
                    new RouteStep
                    {
                        WorldId = input.WorldId,
                        RouteId = route.Id,
                        SequenceIndex = index,
                        LocationId = locationId,
                        DwellHours = 0,
                    }
            )
            .ToArray();

        return new CityPatrolRoute(route, steps);
    }

    private record GuardPatrolAssignment(
        IReadOnlyList<CreatureJob> Jobs,
        Route? Route,
        IReadOnlyList<RouteStep> Steps
    );

    private record CityPatrolRoute(Route Route, IReadOnlyList<RouteStep> Steps);
}
