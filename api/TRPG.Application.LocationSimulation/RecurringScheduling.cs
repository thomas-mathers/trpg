using TRPG.Domain;

namespace TRPG.Application.LocationSimulation;

public static class RecurringScheduling
{
    public static bool HasTriggered(
        int triggerHour,
        DayOfWeek? specificDay,
        TimeSpan lastSyncPlaytime,
        TimeSpan currentPlaytime
    )
    {
        var before = GameClock.GetCurrentInGameDateTime(lastSyncPlaytime).AddHours(-triggerHour);
        var after = GameClock.GetCurrentInGameDateTime(currentPlaytime).AddHours(-triggerHour);

        var daysElapsed = (after.Date - before.Date).Days;
        if (daysElapsed <= 0)
        {
            return false;
        }

        if (specificDay == null)
        {
            return true;
        }

        var daysUntilNextMatch = ((int)specificDay - (int)before.DayOfWeek + 7) % 7;
        if (daysUntilNextMatch == 0)
        {
            daysUntilNextMatch = 7;
        }

        return daysUntilNextMatch <= daysElapsed;
    }
}
