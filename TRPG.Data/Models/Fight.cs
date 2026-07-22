namespace TRPG.Data.Models;

public enum CombatOutcome
{
    Ongoing,
    Victory,
    Defeat,
    Fled,
}

public class Fight
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid PlayerId { get; init; }
    public IReadOnlyList<Guid> CombatantIds { get; init; } = [];
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; set; }
    public CombatOutcome Outcome { get; set; } = CombatOutcome.Ongoing;
}
