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
            LocalMoveStarted started => [started.LocationId],
            LocalMoveCompleted completed => [completed.LocationId],
            LocalMoveInterrupted interrupted => [interrupted.LocationId],
            _ => [],
        };
}
