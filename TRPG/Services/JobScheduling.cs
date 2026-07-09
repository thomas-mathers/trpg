using TRPG.Models;

namespace TRPG.Services;

internal static class JobScheduling {
    // Half-open [StartHour, EndHour) — StartHour == EndHour is always-inactive here, not "all day".
    public static bool IsActiveAtHour(Job job, DayOfWeek weekday, int hour) {
        if (job.SpecificDay != null && job.SpecificDay != weekday) {
            return false;
        }

        return job.StartHour <= job.EndHour
            ? hour >= job.StartHour && hour < job.EndHour
            : hour >= job.StartHour || hour < job.EndHour;
    }
}