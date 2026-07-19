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
    public List<Guid> CombatantIds { get; set; } = [];
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; set; }
    public CombatOutcome Outcome { get; set; } = CombatOutcome.Ongoing;
}
