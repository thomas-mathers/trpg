using NCrontab;
using TRPG.Domain;

namespace TRPG.Application.LocationSimulation;

public static class RecurringScheduling
{
    // Cron over in-game time. The calendar underneath is ordinary: the world's month and day names
    // are a display concern, so weekday and month fields mean what they normally mean.
    public static bool HasTriggered(
        string schedule,
        TimeSpan lastSyncPlaytime,
        TimeSpan currentPlaytime
    )
    {
        var parsed = CrontabSchedule.TryParse(schedule);
        if (parsed == null)
        {
            return false;
        }

        var before = GameClock.GetCurrentInGameDateTime(lastSyncPlaytime);
        var after = GameClock.GetCurrentInGameDateTime(currentPlaytime);

        return parsed.GetNextOccurrence(before) <= after;
    }
}
