using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Generators;

internal record BarracksGuardDutyAssignerInput(
    Guid WorldId,
    Guid CityFactionId,
    Guid GroundFloorLocationId,
    Guid GateLocationId,
    IReadOnlyList<Guid> PatrolWaypoints,
    IReadOnlyList<Bed> Beds,
    IReadOnlyList<Creature> Guards
);

internal record BarracksGuardDutyAssignerResult(
    IReadOnlyList<FactionMember> FactionMembers,
    IReadOnlyList<CreatureJob> Jobs
);

// Guards have no household, so unlike shop staff they get no day-off activities.
internal static class BarracksGuardDutyAssigner
{
    private const int PatrolWaypointsPerGuard = 2;
    private static readonly HourWindow DayShiftHours = new(6, 18);
    private static readonly HourWindow NightShiftHours = new(18, 6);
    private static readonly HourWindow DayShiftSleepHours = new(22, 6);
    private static readonly HourWindow NightShiftSleepHours = new(6, 14);

    internal static BarracksGuardDutyAssignerResult Generate(BarracksGuardDutyAssignerInput input)
    {
        var factionMembers = new List<FactionMember>();
        var jobs = new List<CreatureJob>();

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

            jobs.AddRange(
                i switch
                {
                    0 => AssignPatrol(
                        input,
                        guard.Id,
                        bedLocationId,
                        rotationOffset: 0,
                        isDayShift: true
                    ),
                    1 => AssignGate(input, guard.Id, bedLocationId, isDayShift: true),
                    2 => AssignGate(input, guard.Id, bedLocationId, isDayShift: false),
                    3 or 4 => AssignPatrol(
                        input,
                        guard.Id,
                        bedLocationId,
                        rotationOffset: i - 3,
                        isDayShift: true
                    ),
                    _ => AssignPatrol(
                        input,
                        guard.Id,
                        bedLocationId,
                        rotationOffset: i - 5,
                        isDayShift: false
                    ),
                }
            );
        }

        return new BarracksGuardDutyAssignerResult(factionMembers, jobs);
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

    private static IReadOnlyList<CreatureJob> AssignPatrol(
        BarracksGuardDutyAssignerInput input,
        Guid guardId,
        Guid bedLocationId,
        int rotationOffset,
        bool isDayShift
    )
    {
        var shiftHours = isDayShift ? DayShiftHours : NightShiftHours;
        var sleepHours = isDayShift ? DayShiftSleepHours : NightShiftSleepHours;

        var jobs = new List<CreatureJob>
        {
            CreatureJobGenerator.GenerateSleep(guardId, bedLocationId, input.WorldId, sleepHours),
        };

        var waypoints = input.PatrolWaypoints;
        var waypointCount = Math.Min(PatrolWaypointsPerGuard, waypoints.Count);
        var shiftLength = (shiftHours.End - shiftHours.Start + 24) % 24;
        var blockLength = shiftLength / waypointCount;

        for (var w = 0; w < waypointCount; w++)
        {
            var waypoint = waypoints[(rotationOffset + w) % waypoints.Count];
            var blockStart = (shiftHours.Start + w * blockLength) % 24;
            var blockEnd =
                w == waypointCount - 1 ? shiftHours.End : (blockStart + blockLength) % 24;
            jobs.Add(
                CreatureJobGenerator.GenerateWork(
                    guardId,
                    waypoint,
                    input.WorldId,
                    new HourWindow(blockStart, blockEnd)
                )
            );
        }

        return jobs;
    }
}
