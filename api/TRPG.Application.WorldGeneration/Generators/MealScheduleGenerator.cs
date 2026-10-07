using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class MealScheduleGenerator
{
    private const int MealHours = 1;
    private const int MinimumWorkHoursOnEachSide = 2;

    public static IReadOnlyCollection<CreatureJob> Generate(
        Guid worldId,
        IReadOnlyCollection<Creature> creatures,
        IReadOnlyCollection<CreatureJob> jobs,
        TravelGraph graph,
        double timeScale
    )
    {
        var creaturesById = creatures.ToDictionary(creature => creature.Id);
        var candidates = jobs.GroupBy(job => job.CreatureId)
            .SelectMany(group => BuildCandidates(group.Key, group, creaturesById, graph, timeScale))
            .ToArray();

        return candidates
            .GroupBy(candidate => new WorkShift(
                candidate.Work.LocationId,
                candidate.Work.StartHour,
                candidate.Work.EndHour,
                candidate.Work.SpecificDay
            ))
            .SelectMany(group => GenerateForWorkplace(worldId, group.Key, group.ToArray()))
            .ToArray();
    }

    private static IReadOnlyCollection<MealCandidate> BuildCandidates(
        Guid creatureId,
        IEnumerable<CreatureJob> creatureJobs,
        IReadOnlyDictionary<Guid, Creature> creaturesById,
        TravelGraph graph,
        double timeScale
    )
    {
        if (
            !creaturesById.TryGetValue(creatureId, out var creature)
            || creature.Profession == Profession.Guard
            || creature.MovementSpeed <= 0
        )
        {
            return [];
        }

        var jobs = creatureJobs.ToArray();
        var sleep = jobs.FirstOrDefault(job => job.Action == CreatureJobAction.Sleep);
        if (sleep == null)
        {
            return [];
        }

        return jobs.Where(job =>
                job.Action == CreatureJobAction.Work
                && job.RouteId == null
                && NeedsMealBreak(creature.Profession, job)
            )
            .Select(work => BuildCandidate(creature, sleep, work, graph, timeScale))
            .Where(candidate => candidate != null)
            .Select(candidate => candidate!)
            .ToArray();
    }

    private static bool NeedsMealBreak(Profession? profession, CreatureJob work) =>
        profession != Profession.Innkeeper
        && (profession != Profession.Bartender || work.StartHour <= work.EndHour);

    private static MealCandidate? BuildCandidate(
        Creature creature,
        CreatureJob sleep,
        CreatureJob work,
        TravelGraph graph,
        double timeScale
    )
    {
        var travelHours = ResolveTravelHours(
            graph,
            work.LocationId,
            sleep.LocationId,
            creature.MovementSpeed,
            timeScale
        );
        return travelHours == null
            ? null
            : new MealCandidate(
                creature.Id,
                sleep.LocationId,
                work,
                (int)Math.Ceiling(travelHours.Value)
            );
    }

    private static IReadOnlyCollection<CreatureJob> GenerateForWorkplace(
        Guid worldId,
        WorkShift shift,
        IReadOnlyCollection<MealCandidate> candidates
    )
    {
        var travelHours = candidates.Max(candidate => candidate.TravelHours);
        var shiftHours = Duration(shift.StartHour, shift.EndHour);
        var requiredHours = travelHours * 2 + MealHours;
        if (shiftHours < requiredHours + MinimumWorkHoursOnEachSide * 2)
        {
            return [];
        }

        var absenceStartsAfter = (shiftHours - requiredHours) / 2;
        var mealStart = NormalizeHour(shift.StartHour + absenceStartsAfter + travelHours);
        var mealEnd = NormalizeHour(mealStart + MealHours);
        var hours = new HourWindow(mealStart, mealEnd);
        return candidates
            .Select(candidate =>
                CreatureJobGenerator.GenerateMeal(
                    candidate.CreatureId,
                    candidate.HomeLocationId,
                    worldId,
                    hours,
                    shift.SpecificDay
                )
            )
            .ToArray();
    }

    private static double? ResolveTravelHours(
        TravelGraph graph,
        Guid originLocationId,
        Guid destinationLocationId,
        float movementSpeed,
        double timeScale
    )
    {
        var path = graph.FindShortestPath(originLocationId, null, destinationLocationId);
        if (originLocationId != destinationLocationId && path.Count == 0)
        {
            return null;
        }

        return path.Sum(leg => leg.Distance)
            / InLocationPace.MetersPerGameHour(movementSpeed, timeScale);
    }

    private static int Duration(int startHour, int endHour) =>
        endHour >= startHour ? endHour - startHour : 24 - startHour + endHour;

    private static int NormalizeHour(int hour) => hour % 24;

    private record MealCandidate(
        Guid CreatureId,
        Guid HomeLocationId,
        CreatureJob Work,
        int TravelHours
    );

    private record WorkShift(Guid LocationId, int StartHour, int EndHour, DayOfWeek? SpecificDay);
}
