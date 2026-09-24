using TRPG.Domain.Models;

namespace TRPG.Domain;

public record RouteTimelineStep(
    Guid LocationId,
    Guid? ConnectorId,
    double Distance,
    double DwellHours
);

public abstract record RouteTimelinePosition
{
    public sealed record Pending(Guid LocationId, double HoursUntilStart) : RouteTimelinePosition;

    public sealed record Lingering(Guid LocationId, int StepIndex, double HoursUntilDeparture)
        : RouteTimelinePosition;

    public sealed record InTransit(
        Guid ConnectorId,
        Guid FromLocationId,
        Guid ToLocationId,
        double HoursUntilArrival
    ) : RouteTimelinePosition;

    public sealed record Arrived(Guid LocationId, int StepIndex, TimeSpan ArrivedAtPlaytime)
        : RouteTimelinePosition;
}

public static class RouteTimeline
{
    public static double TotalDurationHours(
        IReadOnlyList<RouteTimelineStep> steps,
        RouteTraversal traversal,
        double speedUnitsPerHour
    )
    {
        Validate(steps, traversal, speedUnitsPerHour);

        return steps.Sum(step =>
            step.ConnectorId == null ? 0 : step.DwellHours + TravelHours(step, speedUnitsPerHour)
        );
    }

    public static RouteTimelinePosition Resolve(
        IReadOnlyList<RouteTimelineStep> steps,
        RouteTraversal traversal,
        double speedUnitsPerHour,
        TimeSpan startedAtPlaytime,
        TimeSpan playtime
    )
    {
        Validate(steps, traversal, speedUnitsPerHour);

        if (playtime < startedAtPlaytime)
        {
            return new RouteTimelinePosition.Pending(
                steps[0].LocationId,
                (startedAtPlaytime - playtime) / GameClock.RealTimePerInGameHour
            );
        }

        var elapsedHours = (playtime - startedAtPlaytime) / GameClock.RealTimePerInGameHour;
        var durationHours = TotalDurationHours(steps, traversal, speedUnitsPerHour);
        if (traversal == RouteTraversal.Cyclic)
        {
            elapsedHours %= durationHours;
        }

        double consumedHours = 0;
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            if (step.ConnectorId == null)
            {
                return new RouteTimelinePosition.Arrived(
                    step.LocationId,
                    index,
                    startedAtPlaytime + GameClock.RealTimePerInGameHour * consumedHours
                );
            }

            if (elapsedHours < step.DwellHours)
            {
                return new RouteTimelinePosition.Lingering(
                    step.LocationId,
                    index,
                    step.DwellHours - elapsedHours
                );
            }
            elapsedHours -= step.DwellHours;
            consumedHours += step.DwellHours;

            var travelHours = TravelHours(step, speedUnitsPerHour);
            if (elapsedHours < travelHours)
            {
                return new RouteTimelinePosition.InTransit(
                    step.ConnectorId.Value,
                    step.LocationId,
                    steps[(index + 1) % steps.Count].LocationId,
                    travelHours - elapsedHours
                );
            }
            elapsedHours -= travelHours;
            consumedHours += travelHours;
        }

        return new RouteTimelinePosition.Lingering(
            steps[0].LocationId,
            StepIndex: 0,
            HoursUntilDeparture: steps[0].DwellHours
        );
    }

    public static double HoursBetween(
        IReadOnlyList<RouteTimelineStep> steps,
        double speedUnitsPerHour,
        int fromStepIndex,
        int toStepIndex
    )
    {
        Validate(steps, RouteTraversal.Cyclic, speedUnitsPerHour);
        ValidateStepIndex(steps, fromStepIndex);
        ValidateStepIndex(steps, toStepIndex);

        double hours = 0;
        var index = fromStepIndex;
        while (index != toStepIndex)
        {
            hours += TravelHours(steps[index], speedUnitsPerHour);
            index = (index + 1) % steps.Count;
            if (index != toStepIndex)
            {
                hours += steps[index].DwellHours;
            }
        }
        return hours;
    }

    public static double HoursUntilNextArrivalAt(
        IReadOnlyList<RouteTimelineStep> steps,
        double speedUnitsPerHour,
        TimeSpan startedAtPlaytime,
        TimeSpan playtime,
        int targetStepIndex
    )
    {
        Validate(steps, RouteTraversal.Cyclic, speedUnitsPerHour);
        ValidateStepIndex(steps, targetStepIndex);

        var targetOffsetHours = steps
            .Take(targetStepIndex)
            .Sum(step => step.DwellHours + TravelHours(step, speedUnitsPerHour));
        if (playtime < startedAtPlaytime)
        {
            return (startedAtPlaytime - playtime) / GameClock.RealTimePerInGameHour
                + targetOffsetHours;
        }

        var position = Resolve(
            steps,
            RouteTraversal.Cyclic,
            speedUnitsPerHour,
            startedAtPlaytime,
            playtime
        );
        if (
            position is RouteTimelinePosition.Lingering lingering
            && lingering.StepIndex == targetStepIndex
        )
        {
            return 0;
        }

        var totalDurationHours = TotalDurationHours(
            steps,
            RouteTraversal.Cyclic,
            speedUnitsPerHour
        );
        var elapsedHours = (playtime - startedAtPlaytime) / GameClock.RealTimePerInGameHour;
        var cyclePositionHours = elapsedHours % totalDurationHours;
        return (targetOffsetHours - cyclePositionHours + totalDurationHours) % totalDurationHours;
    }

    private static void Validate(
        IReadOnlyList<RouteTimelineStep> steps,
        RouteTraversal traversal,
        double speedUnitsPerHour
    )
    {
        if (steps.Count == 0)
        {
            throw new ArgumentException("A route requires at least one step.", nameof(steps));
        }

        if (speedUnitsPerHour <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(speedUnitsPerHour),
                speedUnitsPerHour,
                "Route speed must be positive."
            );
        }

        if (steps.Any(step => step.Distance < 0 || step.DwellHours < 0))
        {
            throw new ArgumentException(
                "Route distances and dwell durations cannot be negative.",
                nameof(steps)
            );
        }

        var terminalIndexes = steps
            .Select((step, index) => (step, index))
            .Where(entry => entry.step.ConnectorId == null)
            .Select(entry => entry.index)
            .ToArray();

        if (traversal == RouteTraversal.Finite)
        {
            if (terminalIndexes.Length != 1 || terminalIndexes[0] != steps.Count - 1)
            {
                throw new ArgumentException(
                    "A finite route requires one connectorless terminal step.",
                    nameof(steps)
                );
            }

            if (steps[^1].Distance != 0 || steps[^1].DwellHours != 0)
            {
                throw new ArgumentException(
                    "A finite route's terminal step cannot have distance or dwell time.",
                    nameof(steps)
                );
            }
        }
        else if (terminalIndexes.Length > 0)
        {
            throw new ArgumentException(
                "A cyclic route cannot contain a terminal step.",
                nameof(steps)
            );
        }

        if (
            traversal == RouteTraversal.Cyclic
            && steps.All(step => step.Distance == 0 && step.DwellHours == 0)
        )
        {
            throw new ArgumentException(
                "A cyclic route requires a positive duration.",
                nameof(steps)
            );
        }
    }

    private static double TravelHours(RouteTimelineStep step, double speedUnitsPerHour) =>
        step.Distance / speedUnitsPerHour;

    private static void ValidateStepIndex(IReadOnlyList<RouteTimelineStep> steps, int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= steps.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(stepIndex));
        }
    }
}
