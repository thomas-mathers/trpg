using TRPG.Domain.Models;

namespace TRPG.Application.Caravans;

public record CaravanStop(Guid LocationId, float DistanceToNextStop);

public abstract record CaravanPosition
{
    public sealed record Lingering(Guid LocationId, int StopIndex, double HoursUntilDeparture)
        : CaravanPosition;

    public sealed record InTransit(Guid FromLocationId, Guid ToLocationId, double HoursUntilArrival)
        : CaravanPosition;
}

// Pure arithmetic over a caravan's fixed loop — no persistence, no DI, so the boundary conditions
// (exact arrival/departure instants, cycle wraparound) can be tested directly.
public static class CaravanCycle
{
    // A CounterClockwise instance walks the same stops backward. The distance for traveling stop
    // i -> i-1 is the same physical leg as the stored i-1 -> i distance, so this just re-derives a
    // "forward" sequence that already encodes the reverse walk — every other method here can stay
    // direction-agnostic as long as callers pass stops through this first.
    public static IReadOnlyList<CaravanStop> ToTravelOrder(
        IReadOnlyList<CaravanStop> stopsInStoredOrder,
        CaravanDirection direction
    )
    {
        if (direction == CaravanDirection.Clockwise)
        {
            return stopsInStoredOrder;
        }

        var count = stopsInStoredOrder.Count;
        return Enumerable
            .Range(0, count)
            .Select(j => new CaravanStop(
                stopsInStoredOrder[(count - j) % count].LocationId,
                stopsInStoredOrder[(count - j - 1 + count) % count].DistanceToNextStop
            ))
            .ToArray();
    }

    public static double TotalCycleHours(
        IReadOnlyList<CaravanStop> stops,
        double lingerHours,
        float speedUnitsPerHour
    ) => stops.Sum(stop => lingerHours + LegHours(stop, speedUnitsPerHour));

    public static CaravanPosition Resolve(
        IReadOnlyList<CaravanStop> stops,
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
                return new CaravanPosition.Lingering(
                    stops[i].LocationId,
                    i,
                    lingerHours - position
                );
            }
            position -= lingerHours;

            var legHours = LegHours(stops[i], speedUnitsPerHour);
            var nextIndex = (i + 1) % stops.Count;
            if (position < legHours)
            {
                return new CaravanPosition.InTransit(
                    stops[i].LocationId,
                    stops[nextIndex].LocationId,
                    legHours - position
                );
            }
            position -= legHours;
        }

        // Floating-point rounding can land exactly on the cycle boundary — treat it as having just
        // arrived back at the first stop rather than falling through with no match.
        return new CaravanPosition.Lingering(stops[0].LocationId, 0, lingerHours);
    }

    // Total hours to travel from one stop to another going forward around the loop, including the
    // linger time at every stop strictly between them — the traveler boards immediately (skipping
    // the origin's remaining linger) and disembarks immediately on arrival (skipping the
    // destination's linger).
    public static double HoursBetween(
        IReadOnlyList<CaravanStop> stops,
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

    private static int LegHours(CaravanStop stop, float speedUnitsPerHour) =>
        Math.Max(1, (int)(stop.DistanceToNextStop / speedUnitsPerHour));
}
