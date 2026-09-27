using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.CreatureJobs;

public static class CreatureJobScheduling
{
    public record ScheduledCreatureJob(
        CreatureJob Job,
        GameInstant StartsAtGameTime,
        bool IsActive
    );

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
        GameInstant gameTime
    )
    {
        var currentDateTime = GameClock.GetCurrentInGameDateTime(gameTime);
        var active = FindDueJob(jobs, currentDateTime.DayOfWeek, currentDateTime.Hour);
        if (active != null)
        {
            var startDateTime = ResolveActiveStart(active, currentDateTime);
            return new ScheduledCreatureJob(
                active,
                gameTime + TimeSpan.FromHours(1) * (startDateTime - currentDateTime).TotalHours,
                IsActive: true
            );
        }

        return jobs.Select(job => ResolveNextStart(job, currentDateTime))
            .Where(entry => entry != null)
            .OrderBy(entry => entry!.Value.StartDateTime)
            .ThenByDescending(entry => entry!.Value.Job.Priority)
            .ThenBy(entry => entry!.Value.Job.Id)
            .Select(entry => new ScheduledCreatureJob(
                entry!.Value.Job,
                gameTime
                    + TimeSpan.FromHours(1)
                        * (entry.Value.StartDateTime - currentDateTime).TotalHours,
                IsActive: false
            ))
            .FirstOrDefault();
    }

    public static GameInstant FindMostRecentEndGameTime(CreatureJob job, GameInstant gameTime)
    {
        var currentDateTime = GameClock.GetCurrentInGameDateTime(gameTime);
        for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            var start = currentDateTime.Date.AddDays(-dayOffset).AddHours(job.StartHour);
            if (job.SpecificDay != null && job.SpecificDay != start.DayOfWeek)
            {
                continue;
            }

            var end = start.AddHours((job.EndHour - job.StartHour + 24) % 24);
            if (end <= currentDateTime)
            {
                return gameTime + TimeSpan.FromHours(1) * (end - currentDateTime).TotalHours;
            }
        }

        throw new InvalidOperationException("A scheduled job has no prior end time.");
    }

    private static DateTime ResolveActiveStart(CreatureJob job, DateTime currentDateTime)
    {
        var start = currentDateTime.Date.AddHours(job.StartHour);
        return job.StartHour > job.EndHour && currentDateTime.Hour < job.EndHour
            ? start.AddDays(-1)
            : start;
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
