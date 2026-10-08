using TRPG.Application.WorldSimulation.Movement;

namespace TRPG.Application.WorldSimulation.Publishing;

internal static class SimEventLocations
{
    public static IEnumerable<Guid> Touched(SimEvent simEvent) =>
        simEvent switch
        {
            JourneyStarted started => [started.OriginLocationId],
            LocationEntered entered => [entered.FromLocationId, entered.ToLocationId],
            JourneyCompleted completed => [completed.LocationId],
            PatrolEnded ended => [ended.LocationId],
            DwellStarted dwell => [dwell.LocationId],
            _ => [],
        };
}
