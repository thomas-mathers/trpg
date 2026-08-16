using TRPG.Data.Models;

namespace TRPG.Application.CreatureJobs;

public static class CreatureJobScheduling
{
    public static bool IsActiveAtHour(CreatureJob creatureJob, DayOfWeek weekday, int hour)
    {
        if (creatureJob.SpecificDay != null && creatureJob.SpecificDay != weekday)
        {
            return false;
        }

        return creatureJob.StartHour <= creatureJob.EndHour
            ? hour >= creatureJob.StartHour && hour < creatureJob.EndHour
            : hour >= creatureJob.StartHour || hour < creatureJob.EndHour;
    }
}
