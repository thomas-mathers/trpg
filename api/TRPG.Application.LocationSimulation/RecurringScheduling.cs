using NCrontab;
using TRPG.Domain;

namespace TRPG.Application.LocationSimulation;

public static class RecurringScheduling
{
    // Cron over in-game time. The calendar underneath is ordinary: the world's month and day names
    // are a display concern, so weekday and month fields mean what they normally mean.
    public static bool HasTriggered(
        string schedule,
        GameInstant lastSyncGameTime,
        GameInstant currentGameTime
    )
    {
        var parsed = CrontabSchedule.TryParse(schedule);
        if (parsed == null)
        {
            return false;
        }

        var before = GameClock.GetCurrentInGameDateTime(lastSyncGameTime);
        var after = GameClock.GetCurrentInGameDateTime(currentGameTime);

        return parsed.GetNextOccurrence(before) <= after;
    }
}
