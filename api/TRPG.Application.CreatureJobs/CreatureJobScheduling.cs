using TRPG.Domain.Models;

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

    public static CreatureJob? FindDueJob(
        IEnumerable<CreatureJob> jobs,
        DayOfWeek weekday,
        int hour
    ) =>
        jobs.Where(job => IsActiveAtHour(job, weekday, hour))
            .OrderByDescending(job => job.Priority)
            .ThenBy(job => job.Id)
            .FirstOrDefault();
}
