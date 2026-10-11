using TRPG.Application.CreatureJobs;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Movement;

internal static class JobTransition
{
    private const int HoursPerWeek = 7 * 24;

    public static GameInstant FindWindowEnd(
        IReadOnlyList<CreatureJob> jobs,
        CreatureJob job,
        GameInstant from
    )
    {
        var firstBoundary = StartOfHour(from);
        for (var offset = 0; offset <= HoursPerWeek; offset++)
        {
            var at = offset == 0 ? from : firstBoundary + TimeSpan.FromHours(offset);
            if (DueJobAt(jobs, at)?.Id != job.Id)
            {
                return at;
            }
        }

        return from + TimeSpan.FromHours(HoursPerWeek);
    }

    private static CreatureJob? DueJobAt(IReadOnlyList<CreatureJob> jobs, GameInstant at) =>
        CreatureJobScheduling.FindDueJob(jobs, at.Value.DayOfWeek, at.Value.Hour);

    private static GameInstant StartOfHour(GameInstant instant) =>
        new(instant.Value.Date.AddHours(instant.Value.Hour));
}
