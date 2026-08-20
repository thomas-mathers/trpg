namespace TRPG.Domain.Models;

public enum CombatOutcome
{
    Ongoing,
    Victory,
    Defeat,
    Fled,
}

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
    public string? LocationName { get; init; }
    public Guid PlayerId { get; init; }
    public EncounterState State { get; set; } = EncounterState.Active;
    public Guid WorldId { get; init; }
}

public record HostileEncounterMemberSnapshot(
    Guid Id,
    string Name,
    CreatureType CreatureType,
    int Level
);

public class HostileEncounter : Encounter
{
    public required Guid FactionId { get; init; }
    public required string FactionName { get; init; }
    public List<HostileEncounterMemberSnapshot> Members { get; init; } = [];
}

public class FightEncounter : Encounter
{
    public List<Guid> CombatantIds { get; init; } = [];
    public CombatOutcome Outcome { get; set; } = CombatOutcome.Ongoing;
}

public class GuardEncounter : Encounter
{
    public required Guid GuardCreatureId { get; init; }
    public required Guid CityFactionId { get; init; }
    public required string GuardName { get; init; }
    public required int ReputationScore { get; init; }
    public required int FineAmount { get; init; }
    public required int JailHours { get; init; }
    public List<string> RecentOffenses { get; init; } = [];
}
