using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal record HourWindow(int Start, int End);

internal static class CreatureJobGenerator
{
    private static readonly HourWindow DefaultSleepHours = new(22, 6);
    private static readonly HourWindow DefaultWorkHours = new(8, 20);
    private static readonly HourWindow IdleHours = new(6, 22);
    private const int DayOffPriority = 60;
    private const int UnemployedActivityPriority = 10;

    public static CreatureJob GenerateSleep(
        Guid stateId,
        Guid creatureId,
        Guid locationId,
        Guid worldId,
        HourWindow? hours = null
    )
    {
        var window = hours ?? DefaultSleepHours;
        return new CreatureJob
        {
            StateId = stateId,
            CreatureId = creatureId,
            Action = CreatureJobAction.Sleep,
            StartHour = window.Start,
            EndHour = window.End,
            Priority = 100,
            LocationId = locationId,
            WorldId = worldId,
        };
    }

    public static CreatureJob GenerateIdle(
        Guid stateId,
        Guid creatureId,
        Guid locationId,
        Guid worldId
    )
    {
        return new CreatureJob
        {
            StateId = stateId,
            CreatureId = creatureId,
            Action = CreatureJobAction.Idle,
            StartHour = IdleHours.Start,
            EndHour = IdleHours.End,
            Priority = 0,
            LocationId = locationId,
            WorldId = worldId,
        };
    }

    public static CreatureJob GenerateWork(
        Guid stateId,
        Guid creatureId,
        Guid locationId,
        Guid worldId,
        HourWindow? hours = null
    )
    {
        var window = hours ?? DefaultWorkHours;
        return new CreatureJob
        {
            StateId = stateId,
            CreatureId = creatureId,
            Action = CreatureJobAction.Work,
            StartHour = window.Start,
            EndHour = window.End,
            Priority = 50,
            LocationId = locationId,
            WorldId = worldId,
        };
    }

    public static CreatureJob GenerateDayOff(
        Guid stateId,
        Guid creatureId,
        CreatureJobAction action,
        Guid locationId,
        DayOfWeek day,
        Guid worldId,
        HourWindow? hours = null
    )
    {
        var window = hours ?? DefaultWorkHours;
        return new CreatureJob
        {
            StateId = stateId,
            CreatureId = creatureId,
            Action = action,
            StartHour = window.Start,
            EndHour = window.End,
            Priority = DayOffPriority,
            LocationId = locationId,
            SpecificDay = day,
            WorldId = worldId,
        };
    }

    public static CreatureJob GenerateUnemployedDayActivity(
        Guid stateId,
        Guid creatureId,
        CreatureJobAction action,
        Guid locationId,
        DayOfWeek day,
        Guid worldId,
        HourWindow? hours = null
    )
    {
        var window = hours ?? IdleHours;
        return new CreatureJob
        {
            StateId = stateId,
            CreatureId = creatureId,
            Action = action,
            StartHour = window.Start,
            EndHour = window.End,
            Priority = UnemployedActivityPriority,
            LocationId = locationId,
            SpecificDay = day,
            WorldId = worldId,
        };
    }

    public static void ApplySleepOverride(
        Guid creatureId,
        HourWindow? sleepHours,
        Guid stateId,
        Guid worldId,
        List<CreatureJob> jobs
    )
    {
        if (sleepHours == null)
        {
            return;
        }

        var existingSleep = jobs.First(j =>
            j.CreatureId == creatureId && j.Action == CreatureJobAction.Sleep
        );
        jobs.Remove(existingSleep);
        jobs.Add(GenerateSleep(stateId, creatureId, existingSleep.LocationId, worldId, sleepHours));
    }

    public static IReadOnlyList<CreatureJob> Generate(
        Guid stateId,
        Guid creatureId,
        Guid sleepLocationId,
        Guid? workLocationId,
        Guid idleLocationId,
        Guid worldId
    )
    {
        var jobs = new List<CreatureJob>
        {
            GenerateSleep(stateId, creatureId, sleepLocationId, worldId),
            GenerateIdle(stateId, creatureId, idleLocationId, worldId),
        };

        if (workLocationId != null)
        {
            jobs.Add(GenerateWork(stateId, creatureId, workLocationId.Value, worldId));
        }

        return jobs;
    }
}
