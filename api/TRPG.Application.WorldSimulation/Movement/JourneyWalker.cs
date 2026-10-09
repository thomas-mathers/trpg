using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Movement;

internal static class JourneyWalker
{
    public static void Begin(SimulatedCreature creature, ICollection<SimEvent> events)
    {
        var journey = creature.Journey!;
        creature.IsWalking = true;
        creature.LastUpdate = creature.NextUpdate;
        events.Add(
            new JourneyStarted(
                creature.Id,
                creature.LastUpdate,
                creature.LocationId,
                journey.DestinationJob.LocationId,
                journey.IsOngoing ? journey.CurrentLeg.ConnectorId : null
            )
        );
    }

    public static void Advance(
        SimulatedCreature creature,
        GameInstant until,
        ICollection<SimEvent> events
    )
    {
        var journey = creature.Journey!;
        var settledAt = WalkLegs(creature, journey, creature.LastUpdate, until, events);
        creature.LastUpdate = until;
        if (journey.IsOngoing)
        {
            creature.NextUpdate = creature.TimeToNextEvent(until);
            return;
        }

        Complete(creature, journey, settledAt, events);
    }

    public static void Resume(SimulatedCreature creature, GameInstant at)
    {
        creature.LastUpdate = at;
        creature.NextUpdate = creature.TimeToNextEvent(at);
    }

    private static GameInstant WalkLegs(
        SimulatedCreature creature,
        Journey journey,
        GameInstant from,
        GameInstant until,
        ICollection<SimEvent> events
    )
    {
        var cursor = from;
        while (journey.IsOngoing)
        {
            var meters = journey.MetersToNextEvent;
            var available =
                Math.Max(0, (until - cursor).TotalSeconds) * creature.MetersPerGameSecond;
            if (available < meters)
            {
                journey.LegWalkedMeters += available;
                break;
            }

            cursor += TimeSpan.FromSeconds(meters / creature.MetersPerGameSecond);
            journey.LegWalkedMeters += meters;
            CrossConnector(creature, journey, cursor, events);
        }

        return cursor;
    }

    private static void CrossConnector(
        SimulatedCreature creature,
        Journey journey,
        GameInstant crossedAt,
        ICollection<SimEvent> events
    )
    {
        var leg = journey.CurrentLeg;
        creature.LocationId = leg.DestinationLocationId;
        creature.CurrentTravelNodeId = leg.ArrivalNodeId;
        journey.LegIndex++;
        journey.LegWalkedMeters = 0;
        events.Add(
            new LocationEntered(
                creature.Id,
                crossedAt,
                leg.OriginLocationId,
                leg.DestinationLocationId,
                leg.ConnectorId,
                journey.IsOngoing ? journey.CurrentLeg.ConnectorId : null
            )
        );
    }

    private static void Complete(
        SimulatedCreature creature,
        Journey journey,
        GameInstant finishedAt,
        ICollection<SimEvent> events
    )
    {
        var job = journey.DestinationJob;
        events.Add(
            new JourneyCompleted(creature.Id, finishedAt, creature.LocationId, job.Id, job.Action)
        );
        creature.Journey = null;
        creature.IsWalking = false;
        creature.NextUpdate =
            journey.WindowStart > creature.LastUpdate ? journey.WindowStart : creature.LastUpdate;
    }
}
