using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Movement;

internal static class MovementConcern
{
    public static IReadOnlyList<SimEvent> Tick(CreatureTickInput input)
    {
        var creature = input.Creature;
        var now = input.Context.Now;
        var events = new List<SimEvent>();
        if (creature.NextUpdate > now || (creature.IsFrozen && creature.LocalMove is null))
        {
            return events;
        }

        if (creature.LocalMove is not null)
        {
            LocalMoveWalker.Advance(creature, now, events);
        }
        else if (creature.IsWalking)
        {
            JourneyWalker.Advance(creature, now, events);
        }
        else if (creature.Journey != null)
        {
            JourneyWalker.Begin(creature, events);
            JourneyWalker.Advance(creature, now, events);
        }

        return events;
    }

    public static IReadOnlyList<SimEvent> StartLocalMove(
        SimulatedCreature creature,
        LocalMovePlan plan,
        GameInstant at
    )
    {
        if (creature.IsFrozen && !CanPursue(creature, plan))
        {
            return [];
        }

        var events = new List<SimEvent>();
        if (creature.LocalMove is not null)
        {
            InterruptLocalMove(creature, at, events);
        }

        creature.LocalMove = new LocalMoveExecution(plan);
        creature.IsWalking = true;
        creature.LastUpdate = at;
        creature.NextUpdate = creature.TimeToLocalMoveEnd(at);
        events.Add(
            new LocalMoveStarted(
                creature.Id,
                at,
                creature.LocationId,
                plan,
                creature.MetersPerGameSecond
            )
        );
        return events;
    }

    private static bool CanPursue(SimulatedCreature creature, LocalMovePlan plan) =>
        plan.TargetKind == LocalMoveTargetKind.Pursuit && creature.Journey is null;

    public static IReadOnlyList<SimEvent> Engage(SimulatedCreature creature, GameInstant at)
    {
        var events = new List<SimEvent>();
        if (creature.IsFrozen)
        {
            return events;
        }

        if (creature.LocalMove is not null)
        {
            InterruptLocalMove(creature, at, events);
        }
        else if (creature.IsWalking)
        {
            CheckpointJourney(creature, at, events);
        }

        creature.IsFrozen = true;
        return events;
    }

    public static IReadOnlyList<SimEvent> Release(SimulatedCreature creature, GameInstant at)
    {
        var events = new List<SimEvent>();
        if (!creature.IsFrozen)
        {
            return events;
        }

        if (creature.LocalMove is not null)
        {
            InterruptLocalMove(creature, at, events);
        }

        creature.IsFrozen = false;
        if (!creature.IsWalking)
        {
            creature.Journey = null;
            creature.NextUpdate = at;
            return events;
        }

        JourneyWalker.Resume(creature, at);
        var journey = creature.Journey!;
        events.Add(
            new JourneyCheckpoint(
                creature.Id,
                at,
                creature.LocationId,
                journey.Id,
                journey.Status,
                journey.LegIndex,
                journey.LegWalkedMeters
            )
        );
        return events;
    }

    private static void InterruptLocalMove(
        SimulatedCreature creature,
        GameInstant at,
        ICollection<SimEvent> events
    )
    {
        LocalMoveWalker.Advance(creature, at, events);
        if (creature.LocalMove is not { } localMove)
        {
            return;
        }

        events.Add(
            new LocalMoveInterrupted(
                creature.Id,
                at,
                creature.LocationId,
                LocalMoveWalker.Position(localMove)
            )
        );
        creature.LocalMove = null;
        creature.IsWalking = false;
    }

    private static void CheckpointJourney(
        SimulatedCreature creature,
        GameInstant at,
        ICollection<SimEvent> events
    )
    {
        JourneyWalker.Advance(creature, at, events);
        if (creature.Journey is not { } journey)
        {
            return;
        }

        events.Add(
            new JourneyCheckpoint(
                creature.Id,
                at,
                creature.LocationId,
                journey.Id,
                journey.Status,
                journey.LegIndex,
                journey.LegWalkedMeters,
                at
            )
        );
    }
}
