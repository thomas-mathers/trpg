namespace TRPG.Domain.Models;

public enum FactDisclosureApproach
{
    Bribe,
    Intimidation,
}

// A failed approach stays unavailable until supporting progress changes the NPC's disposition.
public class FactDisclosureLockout
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid PlayerId { get; init; }
    public Guid NpcId { get; init; }
    public Guid FactId { get; init; }
    public FactDisclosureApproach Approach { get; init; }
}
