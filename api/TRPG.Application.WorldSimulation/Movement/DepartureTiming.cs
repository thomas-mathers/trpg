using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Movement;

internal static class DepartureTiming
{
    private const double LunchJitterHours = 1.0 / 3;
    private const double EndOfShiftJitterHours = 0.5;

    public static GameInstant Resolve(
        Guid creatureId,
        JobTransition transition,
        TimeSpan walkDuration
    )
    {
        var jitter = StableUnitInterval(creatureId, transition);
        if (transition.Destination.Action == CreatureJobAction.Eat)
        {
            return transition.At
                - walkDuration
                + TimeSpan.FromHours((jitter * 2 - 1) * LunchJitterHours);
        }

        if (transition.Origin?.Action == CreatureJobAction.Work)
        {
            return transition.At + TimeSpan.FromHours(jitter * EndOfShiftJitterHours);
        }

        return transition.At - walkDuration;
    }

    private static double StableUnitInterval(Guid creatureId, JobTransition transition)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        var weekHour = (int)transition.At.Value.DayOfWeek * 24 + transition.At.Value.Hour;
        var hash = offset;
        foreach (
            var value in creatureId
                .ToByteArray()
                .Concat((transition.Origin?.Id ?? Guid.Empty).ToByteArray())
                .Concat(transition.Destination.Id.ToByteArray())
                .Concat(BitConverter.GetBytes(weekHour))
        )
        {
            hash = unchecked((hash ^ value) * prime);
        }

        return (hash >> 11) * (1.0 / (1UL << 53));
    }
}
