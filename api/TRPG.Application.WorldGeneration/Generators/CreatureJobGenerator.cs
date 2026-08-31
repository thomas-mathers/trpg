using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record HourWindow(int Start, int End);

internal static class CreatureJobGenerator
{
    private static readonly HourWindow DefaultSleepHours = new(22, 6);
    private static readonly HourWindow DefaultWorkHours = new(8, 20);
    private static readonly HourWindow IdleHours = new(6, 22);
    private const int DayOffPriority = 60;
    private const int UnemployedActivityPriority = 10;

    public static CreatureJob GenerateSleep(
        Guid creatureId,
        Guid locationId,
        Guid worldId,
        HourWindow? hours = null
    )
    {
        var window = hours ?? DefaultSleepHours;
        return new CreatureJob
        {
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
        Guid creatureId,
        Guid locationId,
        Guid worldId,
        HourWindow? hours = null
    )
    {
        var window = hours ?? IdleHours;
        return new CreatureJob
        {
            CreatureId = creatureId,
            Action = CreatureJobAction.Idle,
            StartHour = window.Start,
            EndHour = window.End,
            Priority = 0,
            LocationId = locationId,
            WorldId = worldId,
        };
    }

    public static CreatureJob GenerateWork(
        Guid creatureId,
        Guid locationId,
        Guid worldId,
        HourWindow? hours = null
    )
    {
        var window = hours ?? DefaultWorkHours;
        return new CreatureJob
        {
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
        HourWindow workHours,
        Guid worldId,
        List<CreatureJob> jobs
    )
    {
        if (!Overlaps(workHours, DefaultSleepHours))
        {
            return;
        }

        var existingSleep = jobs.First(j =>
            j.CreatureId == creatureId && j.Action == CreatureJobAction.Sleep
        );
        jobs.Remove(existingSleep);
        var overrideHours = new HourWindow(workHours.End, (workHours.End + 8) % 24);
        jobs.Add(GenerateSleep(creatureId, existingSleep.LocationId, worldId, overrideHours));
    }

    private static bool Overlaps(HourWindow a, HourWindow b)
    {
        bool Contains(HourWindow window, int hour) =>
            window.Start <= window.End
                ? hour >= window.Start && hour < window.End
                : hour >= window.Start || hour < window.End;

        for (var hour = 0; hour < 24; hour++)
        {
            if (Contains(a, hour) && Contains(b, hour))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<CreatureJob> Generate(
        Guid creatureId,
        Guid sleepLocationId,
        Guid? workLocationId,
        Guid idleLocationId,
        Guid worldId
    )
    {
        var jobs = new List<CreatureJob>
        {
            GenerateSleep(creatureId, sleepLocationId, worldId),
            GenerateIdle(creatureId, idleLocationId, worldId),
        };

        if (workLocationId != null)
        {
            jobs.Add(GenerateWork(creatureId, workLocationId.Value, worldId));
        }

        return jobs;
    }
}
