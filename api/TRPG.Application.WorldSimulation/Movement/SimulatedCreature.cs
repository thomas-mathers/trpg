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
    Guid? CurrentTravelNodeId = null,
    JourneyExecutionSeed? Journey = null,
    bool IsEngaged = false
);

public sealed record JourneyExecutionSeed(
    Guid JourneyId,
    JourneyStatus Status,
    GameInstant DepartureAt,
    int CheckpointLegIndex,
    double CheckpointLegProgressMeters,
    GameInstant CheckpointedAt,
    IReadOnlyList<JourneyLeg> Legs,
    CreatureJob? DestinationJob,
    IReadOnlyDictionary<Guid, Guid> LocationIdByNodeId
);

public sealed record SimCreatureState(Guid LocationId, bool IsWalking, bool IsFrozen);

internal sealed class JourneyExecution(JourneyExecutionSeed seed)
{
    public Guid Id { get; } = seed.JourneyId;
    public JourneyStatus Status { get; set; } = seed.Status;
    public GameInstant DepartureAt { get; } = seed.DepartureAt;
    public IReadOnlyList<JourneyLeg> Legs { get; } = seed.Legs;
    public CreatureJob? DestinationJob { get; } = seed.DestinationJob;
    public IReadOnlyDictionary<Guid, Guid> LocationIdByNodeId { get; } = seed.LocationIdByNodeId;
    public int LegIndex { get; set; } = seed.CheckpointLegIndex;
    public double LegWalkedMeters { get; set; } = seed.CheckpointLegProgressMeters;

    public bool IsOngoing => LegIndex < Legs.Count;
    public JourneyLeg CurrentLeg => Legs[LegIndex];
    public double LegRemainingMeters => CurrentLeg.Distance - LegWalkedMeters;
    public double MetersToNextEvent => LegRemainingMeters;

    public Guid LocationOf(Guid nodeId) => LocationIdByNodeId[nodeId];
}

internal sealed class SimulatedCreature
{
    public required Guid Id { get; init; }
    public required double MetersPerGameSecond { get; init; }
    public required IReadOnlyList<CreatureJob> Jobs { get; init; }
    public required Guid LocationId { get; set; }
    public Guid? CurrentTravelNodeId { get; set; }
    public Guid? ShelterLocationId { get; init; }
    public JourneyExecution? Journey { get; set; }
    public bool IsWalking { get; set; }
    public bool IsFrozen { get; set; }
    public GameInstant NextUpdate { get; set; }
    public GameInstant LastUpdate { get; set; }

    public GameInstant TimeToNextEvent(GameInstant from) =>
        from + TimeSpan.FromSeconds(Journey!.MetersToNextEvent / MetersPerGameSecond);

    public SimCreatureState ToState() => new(LocationId, IsWalking, IsFrozen);
}
