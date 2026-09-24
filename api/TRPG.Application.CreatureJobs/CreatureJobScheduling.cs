using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.CreatureJobs;

public static class CreatureJobScheduling
{
    public record ScheduledCreatureJob(CreatureJob Job, TimeSpan StartsAtPlaytime, bool IsActive);

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

    public static ScheduledCreatureJob? FindCurrentOrNextJob(
        IReadOnlyCollection<CreatureJob> jobs,
        TimeSpan playtime
    )
    {
        var currentDateTime = GameClock.GetCurrentInGameDateTime(playtime);
        var active = FindDueJob(jobs, currentDateTime.DayOfWeek, currentDateTime.Hour);
        if (active != null)
        {
            return new ScheduledCreatureJob(active, playtime, IsActive: true);
        }

        return jobs.Select(job => ResolveNextStart(job, currentDateTime))
            .Where(entry => entry != null)
            .OrderBy(entry => entry!.Value.StartDateTime)
            .ThenByDescending(entry => entry!.Value.Job.Priority)
            .ThenBy(entry => entry!.Value.Job.Id)
            .Select(entry => new ScheduledCreatureJob(
                entry!.Value.Job,
                playtime
                    + GameClock.RealTimePerInGameHour
                        * (entry.Value.StartDateTime - currentDateTime).TotalHours,
                IsActive: false
            ))
            .FirstOrDefault();
    }

    private static NextJobStart? ResolveNextStart(CreatureJob job, DateTime currentDateTime)
    {
        for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            var start = currentDateTime.Date.AddDays(dayOffset).AddHours(job.StartHour);
            if (
                start > currentDateTime
                && (job.SpecificDay == null || job.SpecificDay == start.DayOfWeek)
            )
            {
                return new NextJobStart(job, start);
            }
        }

        return null;
    }

    private record struct NextJobStart(CreatureJob Job, DateTime StartDateTime);
}
