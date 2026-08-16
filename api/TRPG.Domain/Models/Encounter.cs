namespace TRPG.Domain.Models;

public enum EncounterState
{
    Active,
    Completed,
}

public abstract class Encounter
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? ArrivalOriginLocationId { get; init; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public Guid LocationId { get; init; }
    public Guid PlayerId { get; init; }
    public EncounterState State { get; set; } = EncounterState.Active;
    public Guid WorldId { get; init; }
}

public class HostileEncounter : Encounter
{
    public required Guid EncounterGroupId { get; init; }
}
