namespace TRPG.Domain.Models;

public enum FactDisclosureApproach
{
    Bribe,
    Intimidation,
}

// Recorded when a bribe or intimidation attempt fails outright, so the player can't just repeat
// the same tactic hoping for a different result under deterministic scoring. Cleared once any
// supporting quest for the fact completes, since that raises the NPC's overall disposition rather
// than validating one specific tactic.
public class FactDisclosureLockout
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid PlayerId { get; init; }
    public Guid NpcId { get; init; }
    public Guid FactId { get; init; }
    public FactDisclosureApproach Approach { get; init; }
}
