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
                journey.IsOngoing ? journey.CurrentLeg.ConnectorId : null,
                journey.IsOngoing ? journey.StopAhead : null
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
        var start = creature.LastUpdate;
        var walkUntil = journey.LoopUntil is { } end && end < until ? end : until;
        var settledAt = WalkLegs(creature, journey, start, walkUntil, events);

        if (journey.LoopUntil is { } loopEnd && loopEnd <= until)
        {
            EndPatrol(creature, loopEnd > start ? loopEnd : start, until, events);
            return;
        }

        creature.LastUpdate = until;
        if (journey.IsOngoing)
        {
            creature.NextUpdate = ClampToPatrolEnd(journey, creature.TimeToNextEvent(until));
            return;
        }

        if (PatrolAfterArrival(creature, journey, settledAt) is { } patrol)
        {
            creature.Journey = patrol;
            creature.LastUpdate = settledAt;
            Advance(creature, until, events);
            return;
        }

        Complete(creature, journey, settledAt, events);
    }

    public static void Resume(SimulatedCreature creature, GameInstant at)
    {
        var journey = creature.Journey!;
        if (journey.DwellEndsAt is { } dwellEnd)
        {
            journey.DwellEndsAt = dwellEnd + (at - creature.LastUpdate);
        }

        creature.LastUpdate = at;
        creature.NextUpdate = ClampToPatrolEnd(journey, creature.TimeToNextEvent(at));
    }

    private static GameInstant WalkLegs(
        SimulatedCreature creature,
        Journey journey,
        GameInstant from,
        GameInstant walkUntil,
        ICollection<SimEvent> events
    )
    {
        var cursor = from;

        while (journey.IsOngoing)
        {
            if (journey.DwellEndsAt is { } dwellEnd)
            {
                if (dwellEnd > walkUntil)
                {
                    break;
                }

                cursor = dwellEnd;
                EndDwell(creature, journey, dwellEnd, events);
                continue;
            }

            var meters = journey.MetersToNextEvent;
            var available =
                Math.Max(0, (walkUntil - cursor).TotalSeconds) * creature.MetersPerGameSecond;
            if (available < meters)
            {
                journey.LegWalkedMeters += available;
                break;
            }

            cursor += TimeSpan.FromSeconds(meters / creature.MetersPerGameSecond);
            journey.LegWalkedMeters += meters;
            if (journey.StopAhead != null)
            {
                StartDwell(creature, journey, cursor, events);
                continue;
            }

            CrossConnector(creature, journey, cursor, events);
        }

        return cursor;
    }

    private static void StartDwell(
        SimulatedCreature creature,
        Journey journey,
        GameInstant at,
        ICollection<SimEvent> events
    )
    {
        journey.StopServed = true;
        journey.DwellEndsAt = at + creature.PatrolDwell;
        events.Add(
            new DwellStarted(
                creature.Id,
                at,
                creature.LocationId,
                journey.CurrentLeg.Stop!.Position
            )
        );
    }

    private static void EndDwell(
        SimulatedCreature creature,
        Journey journey,
        GameInstant at,
        ICollection<SimEvent> events
    )
    {
        journey.DwellEndsAt = null;
        events.Add(
            new JourneyStarted(
                creature.Id,
                at,
                creature.LocationId,
                journey.DestinationJob.LocationId,
                journey.CurrentLeg.ConnectorId
            )
        );
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
        journey.LegIndex++;
        if (journey.IsPatrol && !journey.IsOngoing)
        {
            journey.LegIndex = 0;
        }

        journey.LegWalkedMeters = 0;
        journey.StopServed = false;
        events.Add(
            new LocationEntered(
                creature.Id,
                crossedAt,
                leg.OriginLocationId,
                leg.DestinationLocationId,
                leg.ConnectorId,
                journey.IsOngoing ? journey.CurrentLeg.ConnectorId : null,
                journey.IsOngoing ? journey.StopAhead : null
            )
        );
    }

    private static GameInstant ClampToPatrolEnd(Journey journey, GameInstant instant) =>
        journey.LoopUntil is { } end && end < instant ? end : instant;

    private static Journey? PatrolAfterArrival(
        SimulatedCreature creature,
        Journey journey,
        GameInstant arrivedAt
    ) =>
        creature.PatrolFor(journey.DestinationJob) is { } legs
            ? new Journey(
                legs,
                journey.DestinationJob,
                journey.WindowStart,
                JobTransition.FindWindowEnd(creature.Jobs, journey.DestinationJob, arrivedAt)
            )
            : null;

    private static void EndPatrol(
        SimulatedCreature creature,
        GameInstant endedAt,
        GameInstant until,
        ICollection<SimEvent> events
    )
    {
        events.Add(new PatrolEnded(creature.Id, endedAt, creature.LocationId));
        creature.Journey = null;
        creature.IsWalking = false;
        creature.LastUpdate = until;
        creature.NextUpdate = endedAt;
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
