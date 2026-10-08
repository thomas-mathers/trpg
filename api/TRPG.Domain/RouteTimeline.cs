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

    public sealed record Lingering(
        Guid LocationId,
        int StepIndex,
        double HoursUntilDeparture,
        GameInstant ArrivedAtGameTime
    ) : RouteTimelinePosition;

    public sealed record InTransit(
        Guid ConnectorId,
        Guid FromLocationId,
        Guid ToLocationId,
        double HoursUntilArrival,
        GameInstant? ArrivedAtGameTime,
        GameInstant DepartedAtGameTime
    ) : RouteTimelinePosition;
}

public static class RouteTimeline
{
    public static double TotalDurationHours(
        IReadOnlyList<RouteTimelineStep> steps,
        double speedUnitsPerHour
    )
    {
        Validate(steps, speedUnitsPerHour);

        return steps.Sum(step => step.DwellHours + TravelHours(step, speedUnitsPerHour));
    }

    public static RouteTimelinePosition Resolve(
        IReadOnlyList<RouteTimelineStep> steps,
        double speedUnitsPerHour,
        GameInstant startedAtGameTime,
        GameInstant gameTime
    )
    {
        Validate(steps, speedUnitsPerHour);

        if (gameTime < startedAtGameTime)
        {
            return new RouteTimelinePosition.Pending(
                steps[0].LocationId,
                (startedAtGameTime - gameTime) / TimeSpan.FromHours(1)
            );
        }

        var elapsedHours = (gameTime - startedAtGameTime) / TimeSpan.FromHours(1);
        var durationHours = TotalDurationHours(steps, speedUnitsPerHour);
        var firstLap = elapsedHours < durationHours;
        elapsedHours %= durationHours;

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            if (elapsedHours < step.DwellHours)
            {
                return new RouteTimelinePosition.Lingering(
                    step.LocationId,
                    index,
                    step.DwellHours - elapsedHours,
                    gameTime - TimeSpan.FromHours(1) * elapsedHours
                );
            }
            elapsedHours -= step.DwellHours;

            var travelHours = TravelHours(step, speedUnitsPerHour);
            if (elapsedHours < travelHours)
            {
                var departedAt = gameTime - TimeSpan.FromHours(1) * elapsedHours;
                var startedHere = index == 0 && firstLap;
                return new RouteTimelinePosition.InTransit(
                    step.ConnectorId!.Value,
                    step.LocationId,
                    steps[(index + 1) % steps.Count].LocationId,
                    travelHours - elapsedHours,
                    startedHere ? null : departedAt - TimeSpan.FromHours(1) * step.DwellHours,
                    departedAt
                );
            }
            elapsedHours -= travelHours;
        }

        return new RouteTimelinePosition.Lingering(
            steps[0].LocationId,
            StepIndex: 0,
            HoursUntilDeparture: steps[0].DwellHours,
            ArrivedAtGameTime: gameTime
        );
    }

    public static double HoursBetween(
        IReadOnlyList<RouteTimelineStep> steps,
        double speedUnitsPerHour,
        int fromStepIndex,
        int toStepIndex
    )
    {
        Validate(steps, speedUnitsPerHour);
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
        GameInstant startedAtGameTime,
        GameInstant gameTime,
        int targetStepIndex
    )
    {
        Validate(steps, speedUnitsPerHour);
        ValidateStepIndex(steps, targetStepIndex);

        var targetOffsetHours = steps
            .Take(targetStepIndex)
            .Sum(step => step.DwellHours + TravelHours(step, speedUnitsPerHour));
        if (gameTime < startedAtGameTime)
        {
            return (startedAtGameTime - gameTime) / TimeSpan.FromHours(1) + targetOffsetHours;
        }

        var position = Resolve(steps, speedUnitsPerHour, startedAtGameTime, gameTime);
        if (
            position is RouteTimelinePosition.Lingering lingering
            && lingering.StepIndex == targetStepIndex
        )
        {
            return 0;
        }

        var totalDurationHours = TotalDurationHours(steps, speedUnitsPerHour);
        var elapsedHours = (gameTime - startedAtGameTime) / TimeSpan.FromHours(1);
        var cyclePositionHours = elapsedHours % totalDurationHours;
        return (targetOffsetHours - cyclePositionHours + totalDurationHours) % totalDurationHours;
    }

    private static void Validate(IReadOnlyList<RouteTimelineStep> steps, double speedUnitsPerHour)
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

        if (steps.Any(step => step.ConnectorId == null))
        {
            throw new ArgumentException("A route cannot contain a terminal step.", nameof(steps));
        }

        if (steps.All(step => step.Distance == 0 && step.DwellHours == 0))
        {
            throw new ArgumentException("A route requires a positive duration.", nameof(steps));
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
