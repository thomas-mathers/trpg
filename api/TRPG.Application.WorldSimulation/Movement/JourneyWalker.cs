using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Movement;

internal static class JourneyWalker
{
    public static void Begin(SimulatedCreature creature, ICollection<SimEvent> events)
    {
        var journey = creature.Journey!;
        creature.IsWalking = true;
        journey.Status = JourneyStatus.Traveling;
        creature.LastUpdate =
            journey.DepartureAt > creature.NextUpdate ? journey.DepartureAt : creature.NextUpdate;
        events.Add(
            new JourneyStarted(
                creature.Id,
                creature.LastUpdate,
                creature.LocationId,
                journey.Legs.Count > 0
                    ? journey.LocationOf(journey.Legs[^1].ToNodeId)
                    : creature.LocationId,
                journey.IsOngoing ? journey.CurrentLeg.ConnectorId : null
            )
        );
        Checkpoint(creature, creature.LastUpdate, events);
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
        JourneyExecution journey,
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
        JourneyExecution journey,
        GameInstant crossedAt,
        ICollection<SimEvent> events
    )
    {
        var leg = journey.CurrentLeg;
        var destinationLocationId = journey.LocationOf(leg.ToNodeId);
        var crossedLocation = creature.LocationId != destinationLocationId;
        var originLocationId = creature.LocationId;
        creature.LocationId = destinationLocationId;
        creature.CurrentTravelNodeId = leg.ToNodeId;
        journey.LegIndex++;
        journey.LegWalkedMeters = 0;
        Checkpoint(creature, crossedAt, events);
        if (crossedLocation)
        {
            events.Add(
                new LocationEntered(
                    creature.Id,
                    crossedAt,
                    originLocationId,
                    destinationLocationId,
                    leg.ConnectorId,
                    journey.IsOngoing ? journey.CurrentLeg.ConnectorId : null,
                    ArrivalNodeId: leg.ToNodeId
                )
            );
        }
    }

    private static void Complete(
        SimulatedCreature creature,
        JourneyExecution journey,
        GameInstant finishedAt,
        ICollection<SimEvent> events
    )
    {
        var job = journey.DestinationJob;
        journey.Status = JourneyStatus.Completed;
        Checkpoint(creature, finishedAt, events);
        events.Add(
            new JourneyCompleted(
                creature.Id,
                finishedAt,
                creature.LocationId,
                job?.Id ?? Guid.Empty,
                job?.Action ?? CreatureJobAction.Idle,
                creature.CurrentTravelNodeId,
                journey.DestinationPropId,
                journey.Id
            )
        );
        creature.Journey = null;
        creature.IsWalking = false;
        creature.NextUpdate = creature.LastUpdate;
    }

    private static void Checkpoint(
        SimulatedCreature creature,
        GameInstant at,
        ICollection<SimEvent> events
    )
    {
        var journey = creature.Journey!;
        events.Add(
            new JourneyCheckpoint(
                creature.Id,
                at,
                journey.Id,
                journey.Status,
                journey.LegIndex,
                journey.LegWalkedMeters
            )
        );
    }
}
