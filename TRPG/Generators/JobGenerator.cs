using TRPG.Data.Models;

namespace TRPG.Generators;

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

    // Overrides an employed creature's Work slot on one specific day (a day off, solo or shared as a family activity).
    // Hours default to the standard Work window; pass the creature's actual building-specific Work hours when they differ.
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

    // Overrides an unemployed/homemaker creature's entire waking day, one random activity per weekday.
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
