namespace TRPG.Domain.Models;

public enum ReputationReason
{
    QuestCompleted,
    KilledFactionMember,
    StoleFromFactionMember,
    PaidFineToGuard,
    ServedJailTime,
    WitnessedKilling,
    WitnessedTheft,
    PickedFactionLock,
    WitnessedLockpicking,
    TrespassedOnFactionProperty,
    WitnessedTrespassing,
    CaughtSneaking,
    CaughtFleeingSuspicion,
}

public class ReputationLogEntry
{
    public Guid CreatureId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string? Detail { get; init; }
    public int DeltaScore { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public required ReputationReason Reason { get; init; }
    public Guid TargetId { get; init; }
    public ReputationTargetType TargetType { get; init; }
    public Guid WorldId { get; init; }
}
