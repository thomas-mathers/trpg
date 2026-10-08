using TRPG.Application.Common.Navigation;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Movement;

public sealed record SimCreatureSeed(
    Guid CreatureId,
    Guid LocationId,
    float MovementSpeed,
    IReadOnlyList<CreatureJob> Jobs,
    bool SeeksShelter = true,
    IReadOnlyDictionary<Guid, IReadOnlyList<RouteLeg>>? Patrols = null
);

public sealed record SimCreatureState(Guid LocationId, bool IsWalking, bool IsFrozen);

internal sealed class Journey(
    IReadOnlyList<RouteLeg> legs,
    CreatureJob destinationJob,
    GameInstant windowStart,
    GameInstant? loopUntil = null
)
{
    public IReadOnlyList<RouteLeg> Legs { get; } = legs;
    public CreatureJob DestinationJob { get; } = destinationJob;
    public GameInstant WindowStart { get; } = windowStart;
    public GameInstant? LoopUntil { get; } = loopUntil;
    public int LegIndex { get; set; }
    public double LegWalkedMeters { get; set; }
    public bool StopServed { get; set; }
    public GameInstant? DwellEndsAt { get; set; }

    public bool IsPatrol => LoopUntil != null;
    public bool IsOngoing => LegIndex < Legs.Count;
    public RouteLeg CurrentLeg => Legs[LegIndex];
    public double LegRemainingMeters => CurrentLeg.Distance - LegWalkedMeters;
    public Point? StopAhead => CurrentLeg.Stop is { } stop && !StopServed ? stop.Position : null;

    public double MetersToNextEvent =>
        CurrentLeg.Stop is { } stop && !StopServed
            ? Math.Max(0, stop.AtMeters - LegWalkedMeters)
            : LegRemainingMeters;
}

internal sealed class SimulatedCreature
{
    public required Guid Id { get; init; }
    public required double MetersPerGameSecond { get; init; }
    public required TimeSpan PatrolDwell { get; init; }
    public required IReadOnlyList<CreatureJob> Jobs { get; init; }
    public required Guid LocationId { get; set; }
    public Guid? ShelterLocationId { get; init; }
    public IReadOnlyDictionary<Guid, IReadOnlyList<RouteLeg>> Patrols { get; init; } =
        new Dictionary<Guid, IReadOnlyList<RouteLeg>>();
    public Journey? Journey { get; set; }
    public bool IsWalking { get; set; }
    public bool IsFrozen { get; set; }
    public GameInstant NextUpdate { get; set; }
    public GameInstant LastUpdate { get; set; }

    public IReadOnlyList<RouteLeg>? PatrolFor(CreatureJob job) =>
        job.RouteId is { } routeId ? Patrols.GetValueOrDefault(routeId) : null;

    public GameInstant TimeToNextEvent(GameInstant from) =>
        Journey!.DwellEndsAt
        ?? from + TimeSpan.FromSeconds(Journey.MetersToNextEvent / MetersPerGameSecond);

    public SimCreatureState ToState() => new(LocationId, IsWalking, IsFrozen);
}
