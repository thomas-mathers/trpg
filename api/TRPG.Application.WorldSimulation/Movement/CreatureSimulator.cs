using TRPG.Application.Common.Navigation;
using TRPG.Application.CreatureJobs;
using TRPG.Application.WorldSimulation.LocalActivities;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Movement;

public sealed record CreatureSimulatorOptions(
    double TimeScale,
    int RoutinePlansPerTick,
    TimeSpan ArrivalStagger
);

public sealed class CreatureSimulator
{
    private readonly CreatureSimulatorOptions _options;
    private IReadOnlySet<Guid> _severeWeatherLocationIds = new HashSet<Guid>();
    private readonly List<SimulatedCreature> _creatures = [];
    private readonly Dictionary<Guid, SimulatedCreature> _creaturesById = [];

    public CreatureSimulator(CreatureSimulatorOptions options)
    {
        _options = options;
    }

    public IReadOnlySet<Guid> IdleLocationIds() =>
        _creatures
            .SelectMany(creature => creature.Jobs)
            .Where(job => job.Action == CreatureJobAction.Idle)
            .Select(job => job.LocationId)
            .ToHashSet();

    public IReadOnlyList<Guid> CreaturesAwaitingRoutinePlanning(GameInstant now) =>
        _creatures
            .Where(creature =>
                !creature.IsFrozen
                && !creature.IsWalking
                && creature.Journey is null
                && creature.NextUpdate <= now
            )
            .Select(creature => creature.Id)
            .ToArray();

    public CreatureSimulatorOptions Options => _options;

    public IReadOnlySet<Guid> SevereWeatherLocationIds => _severeWeatherLocationIds;

    public Guid? ShelterLocationOf(Guid creatureId) =>
        _creaturesById.GetValueOrDefault(creatureId)?.ShelterLocationId;

    public void SetSevereWeatherLocations(
        IReadOnlySet<Guid> severeWeatherLocationIds,
        GameInstant now
    )
    {
        if (_severeWeatherLocationIds.SetEquals(severeWeatherLocationIds))
        {
            return;
        }

        _severeWeatherLocationIds = severeWeatherLocationIds;

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

    public void DeferUntilNextRoutineChange(Guid creatureId, GameInstant now)
    {
        if (_creaturesById.GetValueOrDefault(creatureId) is not { } creature || creature.IsWalking)
        {
            return;
        }

        creature.NextUpdate = NextRoutineChange(creature.Jobs, now);
    }

    public IReadOnlyList<SimEvent> StartLocalMove(LocalMovePlan plan, GameInstant at) =>
        _creaturesById.TryGetValue(plan.CreatureId, out var creature)
            ? MovementConcern.StartLocalMove(creature, plan, at)
            : [];

    public IReadOnlyList<SimEvent> Step(GameInstant now)
    {
        var context = new SimulationContext(now, _severeWeatherLocationIds);
        return
        [
            .. _creatures.SelectMany(creature =>
                MovementConcern.Tick(new CreatureTickInput(creature, context))
            ),
        ];
    }

    public IReadOnlyList<SimEvent> Engage(Guid creatureId, GameInstant at) =>
        _creaturesById.TryGetValue(creatureId, out var creature)
            ? MovementConcern.Engage(creature, at)
            : [];

    public IReadOnlyList<SimEvent> Release(Guid creatureId, GameInstant at) =>
        _creaturesById.TryGetValue(creatureId, out var creature)
            ? MovementConcern.Release(creature, at)
            : [];

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
