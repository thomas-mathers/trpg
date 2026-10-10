using TRPG.Application.Common.Navigation;
using TRPG.Application.CreatureJobs;
using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Movement;

public sealed record WorldSimulatorOptions(
    double TimeScale,
    int RouteSearchesPerTick,
    TimeSpan ArrivalStagger
);

public sealed class WorldSimulator
{
    private readonly WorldSimulatorOptions _options;
    private readonly WeatherShelter _shelter = new();
    private readonly List<SimulatedCreature> _creatures = [];
    private readonly Dictionary<Guid, SimulatedCreature> _creaturesById = [];

    public WorldSimulator(TravelGraph graph, WorldSimulatorOptions options)
    {
        _options = options;
    }

    public IReadOnlySet<Guid> IdleLocationIds() =>
        _creatures
            .SelectMany(creature => creature.Jobs)
            .Where(job => job.Action == CreatureJobAction.Idle)
            .Select(job => job.LocationId)
            .ToHashSet();

    public IReadOnlyList<Guid> CreaturesAwaitingJourneyPlanning(GameInstant now) =>
        _creatures
            .Where(creature =>
                !creature.IsFrozen
                && !creature.IsWalking
                && creature.Journey is null
                && creature.NextUpdate <= now
            )
            .Select(creature => creature.Id)
            .ToArray();

    public WorldSimulatorOptions Options => _options;

    public void SetExposedLocations(IReadOnlySet<Guid> exposedLocationIds, GameInstant now)
    {
        if (!_shelter.Update(exposedLocationIds))
        {
            return;
        }

        foreach (var creature in _creatures.Where(creature => !creature.IsWalking))
        {
            if (creature.Journey is null && !creature.IsFrozen)
            {
                creature.NextUpdate = now;
            }
        }
    }

    public void Add(SimCreatureSeed seed, GameInstant now)
    {
        if (seed.MovementSpeed <= 0)
        {
            return;
        }

        var creature = new SimulatedCreature
        {
            Id = seed.CreatureId,
            LocationId = seed.LocationId,
            CurrentTravelNodeId = seed.CurrentTravelNodeId,
            Jobs = seed.Jobs,
            ShelterLocationId = seed.SeeksShelter
                ? seed.Jobs.FirstOrDefault(job => job.Action == CreatureJobAction.Sleep)?.LocationId
                : null,
            MetersPerGameSecond = InLocationPace.MetersPerGameSecond(
                seed.MovementSpeed,
                _options.TimeScale
            ),
            NextUpdate = now,
            LastUpdate = now,
            IsFrozen = seed.IsEngaged,
        };
        if (seed.Journey is { } journey)
        {
            creature.Journey = new JourneyExecution(journey);
            creature.IsWalking = journey.Status == JourneyStatus.Traveling;
            creature.LastUpdate = journey.CheckpointedAt;
            creature.NextUpdate =
                journey.Status == JourneyStatus.Planned
                    ? journey.DepartureAt
                    : journey.CheckpointedAt;
        }
        _creatures.Add(creature);
        _creaturesById[creature.Id] = creature;
    }

    public void Remove(Guid creatureId)
    {
        if (_creaturesById.Remove(creatureId, out var creature))
        {
            _creatures.Remove(creature);
        }
    }

    public SimCreatureState? StateOf(Guid creatureId) =>
        _creaturesById.GetValueOrDefault(creatureId)?.ToState();

    public bool IsSchedulerSleeping(Guid creatureId, GameInstant now) =>
        _creaturesById.GetValueOrDefault(creatureId) is { } creature
        && !creature.IsWalking
        && !creature.IsFrozen
        && creature.NextUpdate > now;

    public void SleepUntilNextRoutineChange(Guid creatureId, GameInstant now)
    {
        if (_creaturesById.GetValueOrDefault(creatureId) is not { } creature || creature.IsWalking)
        {
            return;
        }

        creature.NextUpdate = NextRoutineChange(creature.Jobs, now);
    }

    public IReadOnlyList<SimEvent> StartLocalMove(LocalMovePlan plan, GameInstant at)
    {
        if (!_creaturesById.TryGetValue(plan.CreatureId, out var creature) || creature.IsFrozen)
        {
            return [];
        }

        creature.LocalMove = new LocalMoveExecution(plan);
        creature.IsWalking = true;
        creature.LastUpdate = at;
        creature.NextUpdate = creature.TimeToLocalMoveEnd(at);
        return
        [
            new LocalMoveStarted(
                creature.Id,
                at,
                creature.LocationId,
                plan,
                creature.MetersPerGameSecond
            ),
        ];
    }

    public IReadOnlyList<SimEvent> Step(GameInstant now)
    {
        var events = new List<SimEvent>();
        foreach (var creature in _creatures)
        {
            if (creature.IsFrozen || creature.NextUpdate > now)
            {
                continue;
            }

            Step(creature, now, events);
        }

        return events;
    }

    public IReadOnlyList<SimEvent> Engage(Guid creatureId, GameInstant at)
    {
        var events = new List<SimEvent>();
        if (!_creaturesById.TryGetValue(creatureId, out var creature) || creature.IsFrozen)
        {
            return events;
        }

        if (creature.LocalMove is not null)
        {
            LocalMoveWalker.Advance(creature, at, events);
            if (creature.LocalMove is { } localMove)
            {
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
        }
        else if (creature.IsWalking)
        {
            JourneyWalker.Advance(creature, at, events);
            if (creature.Journey is { } journey)
            {
                events.Add(
                    new JourneyCheckpoint(
                        creature.Id,
                        at,
                        journey.Id,
                        journey.Status,
                        journey.LegIndex,
                        journey.LegWalkedMeters,
                        at
                    )
                );
            }
        }

        creature.IsFrozen = true;
        return events;
    }

    public IReadOnlyList<SimEvent> Release(Guid creatureId, GameInstant at)
    {
        var events = new List<SimEvent>();
        if (!_creaturesById.TryGetValue(creatureId, out var creature) || !creature.IsFrozen)
        {
            return events;
        }

        creature.IsFrozen = false;
        if (creature.IsWalking)
        {
            JourneyWalker.Resume(creature, at);
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
            return events;
        }

        creature.Journey = null;
        creature.NextUpdate = at;
        return events;
    }

    private void Step(SimulatedCreature creature, GameInstant now, ICollection<SimEvent> events)
    {
        if (creature.LocalMove is not null)
        {
            LocalMoveWalker.Advance(creature, now, events);
            return;
        }

        if (creature.IsWalking)
        {
            JourneyWalker.Advance(creature, now, events);
            return;
        }

        if (creature.Journey != null && creature.NextUpdate <= now)
        {
            JourneyWalker.Begin(creature, events);
            JourneyWalker.Advance(creature, now, events);
        }
    }

    private static GameInstant NextRoutineChange(
        IReadOnlyCollection<CreatureJob> jobs,
        GameInstant now
    )
    {
        var scheduled = CreatureJobScheduling.FindCurrentOrNextJob(jobs, now);
        if (scheduled is null)
        {
            return new GameInstant(DateTime.MaxValue);
        }

        return scheduled.IsActive
            ? JobTransition.FindWindowEnd(jobs.ToArray(), scheduled.Job, now)
            : scheduled.StartsAtGameTime;
    }
}
