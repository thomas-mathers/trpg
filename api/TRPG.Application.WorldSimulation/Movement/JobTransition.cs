using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Movement;

internal sealed record JobTransition(
    CreatureJob Destination,
    CreatureJob? Origin,
    GameInstant At,
    bool IsOpen
)
{
    private const int HoursPerWeek = 7 * 24;

    public static JobTransition? FindNext(
        IReadOnlyList<CreatureJob> jobs,
        Guid currentLocationId,
        GameInstant now,
        Func<CreatureJob, CreatureJob> resolveDestination
    )
    {
        var firstBoundary = StartOfHour(now);
        for (var offset = 0; offset <= HoursPerWeek; offset++)
        {
            var at = offset == 0 ? now : firstBoundary + TimeSpan.FromHours(offset);
            var due = DueJobAt(jobs, at) is { } scheduled ? resolveDestination(scheduled) : null;
            if (due != null && due.LocationId != currentLocationId)
            {
                return new JobTransition(
                    due,
                    DueJobAt(jobs, at - TimeSpan.FromHours(1)),
                    at,
                    offset == 0
                );
            }
        }

        return null;
    }

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
