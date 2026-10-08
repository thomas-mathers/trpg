using TRPG.Application.Common.Navigation;
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
    private const double PatrolDwellRealSeconds = 90;

    private static readonly Dictionary<Guid, IReadOnlyList<RouteLeg>> EmptyPatrols = [];

    private readonly WorldSimulatorOptions _options;
    private readonly RouteFinder _routeFinder;
    private readonly WeatherShelter _shelter = new();
    private readonly JourneyPlanner _planner;
    private readonly List<SimulatedCreature> _creatures = [];
    private readonly Dictionary<Guid, SimulatedCreature> _creaturesById = [];

    public WorldSimulator(TravelGraph graph, WorldSimulatorOptions options)
    {
        _options = options;
        _routeFinder = new RouteFinder(graph, options.RouteSearchesPerTick);
        _planner = new JourneyPlanner(_routeFinder, _shelter, options.ArrivalStagger);
    }

    public IReadOnlySet<Guid> IdleLocationIds() =>
        _creatures
            .SelectMany(creature => creature.Jobs)
            .Where(job => job.Action == CreatureJobAction.Idle)
            .Select(job => job.LocationId)
            .ToHashSet();

    public void SetExposedLocations(IReadOnlySet<Guid> exposedLocationIds, GameInstant now)
    {
        if (!_shelter.Update(exposedLocationIds))
        {
            return;
        }

        foreach (var creature in _creatures.Where(creature => !creature.IsWalking))
        {
            creature.Journey = null;
            if (!creature.IsFrozen)
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
            Jobs = seed.Jobs,
            Patrols = (seed.Patrols ?? EmptyPatrols)
                .Where(patrol => patrol.Value.Sum(leg => leg.Distance) > 0)
                .ToDictionary(),
            ShelterLocationId = seed.SeeksShelter
                ? seed.Jobs.FirstOrDefault(job => job.Action == CreatureJobAction.Sleep)?.LocationId
                : null,
            MetersPerGameSecond = InLocationPace.MetersPerGameSecond(
                seed.MovementSpeed,
                _options.TimeScale
            ),
            PatrolDwell = TimeSpan.FromSeconds(PatrolDwellRealSeconds * _options.TimeScale),
            NextUpdate = now,
            LastUpdate = now,
        };
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

    public IReadOnlyList<SimEvent> Step(GameInstant now)
    {
        var events = new List<SimEvent>();
        _routeFinder.BeginTick();
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

        if (creature.IsWalking)
        {
            JourneyWalker.Advance(creature, at, events);
        }

        creature.IsFrozen = true;
        return events;
    }

    public void Release(Guid creatureId, GameInstant at)
    {
        if (!_creaturesById.TryGetValue(creatureId, out var creature) || !creature.IsFrozen)
        {
            return;
        }

        creature.IsFrozen = false;
        if (creature.IsWalking)
        {
            JourneyWalker.Resume(creature, at);
            return;
        }

        creature.Journey = null;
        creature.NextUpdate = at;
    }

    private void Step(SimulatedCreature creature, GameInstant now, ICollection<SimEvent> events)
    {
        if (creature.IsWalking)
        {
            JourneyWalker.Advance(creature, now, events);
            return;
        }

        if (creature.Journey == null)
        {
            _planner.Plan(creature, now);
        }

        if (creature.Journey != null && creature.NextUpdate <= now)
        {
            JourneyWalker.Begin(creature, events);
            JourneyWalker.Advance(creature, now, events);
        }
    }
}
