using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal record HourWindow(int Start, int End);

internal static class JobGenerator
{
    private static readonly HourWindow DefaultSleepHours = new(22, 6);
    private static readonly HourWindow DefaultWorkHours = new(8, 20);
    private static readonly HourWindow IdleHours = new(6, 22);
    private const int DayOffPriority = 60;
    private const int UnemployedActivityPriority = 10;

    public static Job GenerateSleep(
        Guid stateId,
        Guid creatureId,
        Guid roomId,
        Guid worldId,
        HourWindow? hours = null
    )
    {
        var window = hours ?? DefaultSleepHours;
        return new Job
        {
            StateId = stateId,
            CreatureId = creatureId,
            Action = JobAction.Sleep,
            StartHour = window.Start,
            EndHour = window.End,
            Priority = 100,
            RoomId = roomId,
            WorldId = worldId,
        };
    }

    public static Job GenerateIdle(Guid stateId, Guid creatureId, Guid? roomId, Guid worldId)
    {
        return new Job
        {
            StateId = stateId,
            CreatureId = creatureId,
            Action = JobAction.Idle,
            StartHour = IdleHours.Start,
            EndHour = IdleHours.End,
            Priority = 0,
            RoomId = roomId,
            WorldId = worldId,
        };
    }

    public static Job GenerateWork(
        Guid stateId,
        Guid creatureId,
        Guid roomId,
        Guid worldId,
        HourWindow? hours = null
    )
    {
        var window = hours ?? DefaultWorkHours;
        return new Job
        {
            StateId = stateId,
            CreatureId = creatureId,
            Action = JobAction.Work,
            StartHour = window.Start,
            EndHour = window.End,
            Priority = 50,
            RoomId = roomId,
            WorldId = worldId,
        };
    }

    public static Job GenerateDayOff(
        Guid stateId,
        Guid creatureId,
        JobAction action,
        Guid? roomId,
        DayOfWeek day,
        Guid worldId,
        HourWindow? hours = null
    )
    {
        var window = hours ?? DefaultWorkHours;
        return new Job
        {
            StateId = stateId,
            CreatureId = creatureId,
            Action = action,
            StartHour = window.Start,
            EndHour = window.End,
            Priority = DayOffPriority,
            RoomId = roomId,
            SpecificDay = day,
            WorldId = worldId,
        };
    }

    public static Job GenerateUnemployedDayActivity(
        Guid stateId,
        Guid creatureId,
        JobAction action,
        Guid? roomId,
        DayOfWeek day,
        Guid worldId
    )
    {
        return new Job
        {
            StateId = stateId,
            CreatureId = creatureId,
            Action = action,
            StartHour = IdleHours.Start,
            EndHour = IdleHours.End,
            Priority = UnemployedActivityPriority,
            RoomId = roomId,
            SpecificDay = day,
            WorldId = worldId,
        };
    }

    public static void ApplySleepOverride(
        Guid creatureId,
        HourWindow? sleepHours,
        Guid stateId,
        Guid worldId,
        List<Job> jobs
    )
    {
        if (sleepHours == null)
        {
            return;
        }

        var existingSleep = jobs.First(j =>
            j.CreatureId == creatureId && j.Action == JobAction.Sleep
        );
        jobs.Remove(existingSleep);
        jobs.Add(
            GenerateSleep(stateId, creatureId, existingSleep.RoomId!.Value, worldId, sleepHours)
        );
    }

    public static IReadOnlyList<Job> Generate(
        Guid stateId,
        Guid creatureId,
        Guid sleepRoomId,
        Guid? workRoomId,
        Guid? idleRoomId,
        Guid worldId
    )
    {
        var jobs = new List<Job>
        {
            GenerateSleep(stateId, creatureId, sleepRoomId, worldId),
            GenerateIdle(stateId, creatureId, idleRoomId, worldId),
        };

        if (workRoomId != null)
        {
            jobs.Add(GenerateWork(stateId, creatureId, workRoomId.Value, worldId));
        }

        return jobs;
    }
}
