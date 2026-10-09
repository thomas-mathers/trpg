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
    Guid? CurrentTravelNodeId = null
);

public sealed record SimCreatureState(Guid LocationId, bool IsWalking, bool IsFrozen);

internal sealed class Journey(
    IReadOnlyList<RouteLeg> legs,
    CreatureJob destinationJob,
    GameInstant windowStart
)
{
    public IReadOnlyList<RouteLeg> Legs { get; } = legs;
    public CreatureJob DestinationJob { get; } = destinationJob;
    public GameInstant WindowStart { get; } = windowStart;
    public int LegIndex { get; set; }
    public double LegWalkedMeters { get; set; }

    public bool IsOngoing => LegIndex < Legs.Count;
    public RouteLeg CurrentLeg => Legs[LegIndex];
    public double LegRemainingMeters => CurrentLeg.Distance - LegWalkedMeters;
    public double MetersToNextEvent => LegRemainingMeters;
}

internal sealed class SimulatedCreature
{
    public required Guid Id { get; init; }
    public required double MetersPerGameSecond { get; init; }
    public required IReadOnlyList<CreatureJob> Jobs { get; init; }
    public required Guid LocationId { get; set; }
    public Guid? CurrentTravelNodeId { get; set; }
    public Guid? ShelterLocationId { get; init; }
    public Journey? Journey { get; set; }
    public bool IsWalking { get; set; }
    public bool IsFrozen { get; set; }
    public GameInstant NextUpdate { get; set; }
    public GameInstant LastUpdate { get; set; }

    public GameInstant TimeToNextEvent(GameInstant from) =>
        from + TimeSpan.FromSeconds(Journey!.MetersToNextEvent / MetersPerGameSecond);

    public SimCreatureState ToState() => new(LocationId, IsWalking, IsFrozen);
}
