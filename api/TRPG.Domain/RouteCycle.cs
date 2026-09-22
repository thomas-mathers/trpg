using TRPG.Domain.Models;

namespace TRPG.Domain;

public record RouteWaypoint(Guid LocationId, float DistanceToNextStop);

public abstract record RoutePosition
{
    public sealed record Lingering(Guid LocationId, int StopIndex, double HoursUntilDeparture)
        : RoutePosition;

    public sealed record InTransit(Guid FromLocationId, Guid ToLocationId, double HoursUntilArrival)
        : RoutePosition;
}

// Pure arithmetic over a traveler's fixed loop — no persistence, no DI, so the boundary conditions
// (exact arrival/departure instants, cycle wraparound) can be tested directly.
public static class RouteCycle
{
    // A CounterClockwise instance walks the same stops backward. The distance for traveling stop
    // i -> i-1 is the same physical leg as the stored i-1 -> i distance, so this just re-derives a
    // "forward" sequence that already encodes the reverse walk — every other method here can stay
    // direction-agnostic as long as callers pass stops through this first.
    public static IReadOnlyList<RouteWaypoint> ToTravelOrder(
        IReadOnlyList<RouteWaypoint> stopsInStoredOrder,
        RouteDirection direction
    )
    {
        if (direction == RouteDirection.Clockwise)
        {
            return stopsInStoredOrder;
        }

        var count = stopsInStoredOrder.Count;
        return Enumerable
            .Range(0, count)
            .Select(j => new RouteWaypoint(
                stopsInStoredOrder[(count - j) % count].LocationId,
                stopsInStoredOrder[(count - j - 1 + count) % count].DistanceToNextStop
            ))
            .ToArray();
    }

    public static double TotalCycleHours(
        IReadOnlyList<RouteWaypoint> stops,
        double lingerHours,
        float speedUnitsPerHour
    ) => stops.Sum(stop => lingerHours + LegHours(stop, speedUnitsPerHour));

    public static RoutePosition Resolve(
        IReadOnlyList<RouteWaypoint> stops,
        double lingerHours,
        float speedUnitsPerHour,
        double elapsedHours
    )
    {
        var totalCycleHours = TotalCycleHours(stops, lingerHours, speedUnitsPerHour);
        var position = ((elapsedHours % totalCycleHours) + totalCycleHours) % totalCycleHours;

        for (var i = 0; i < stops.Count; i++)
        {
            if (position < lingerHours)
            {
                return new RoutePosition.Lingering(stops[i].LocationId, i, lingerHours - position);
            }
            position -= lingerHours;

            var legHours = LegHours(stops[i], speedUnitsPerHour);
            var nextIndex = (i + 1) % stops.Count;
            if (position < legHours)
            {
                return new RoutePosition.InTransit(
                    stops[i].LocationId,
                    stops[nextIndex].LocationId,
                    legHours - position
                );
            }
            position -= legHours;
        }

        // Floating-point rounding can land exactly on the cycle boundary — treat it as having just
        // arrived back at the first stop rather than falling through with no match.
        return new RoutePosition.Lingering(stops[0].LocationId, 0, lingerHours);
    }

    // Total hours to travel from one stop to another going forward around the loop, including the
    // linger time at every stop strictly between them — the traveler boards immediately (skipping
    // the origin's remaining linger) and disembarks immediately on arrival (skipping the
    // destination's linger).
    public static double HoursBetween(
        IReadOnlyList<RouteWaypoint> stops,
        double lingerHours,
        float speedUnitsPerHour,
        int fromStopIndex,
        int toStopIndex
    )
    {
        double hours = 0;
        var index = fromStopIndex;
        while (index != toStopIndex)
        {
            hours += LegHours(stops[index], speedUnitsPerHour);
            index = (index + 1) % stops.Count;
            if (index != toStopIndex)
            {
                hours += lingerHours;
            }
        }

        return hours;
    }

    // Hours from now until this instance is next lingering at the given stop — 0 if it's already
    // there, otherwise the forward distance to that stop's next linger window, wrapping around the
    // loop if the current position has already passed it this cycle.
    public static double HoursUntilNextArrivalAt(
        IReadOnlyList<RouteWaypoint> stops,
        double lingerHours,
        float speedUnitsPerHour,
        double elapsedHours,
        int targetStopIndex
    )
    {
        var totalCycleHours = TotalCycleHours(stops, lingerHours, speedUnitsPerHour);
        var position = ((elapsedHours % totalCycleHours) + totalCycleHours) % totalCycleHours;

        double cumulative = 0;
        for (var i = 0; i < stops.Count; i++)
        {
            if (i == targetStopIndex)
            {
                if (position >= cumulative && position < cumulative + lingerHours)
                {
                    return 0;
                }

                return (cumulative - position + totalCycleHours) % totalCycleHours;
            }

            cumulative += lingerHours + LegHours(stops[i], speedUnitsPerHour);
        }

        throw new ArgumentOutOfRangeException(
            nameof(targetStopIndex),
            targetStopIndex,
            "Target stop index is outside the route's stop list."
        );
    }

    private static int LegHours(RouteWaypoint stop, float speedUnitsPerHour) =>
        Math.Max(1, (int)(stop.DistanceToNextStop / speedUnitsPerHour));
}
