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
    public int RoundsResolved { get; set; }
    public bool HasSurpriseRound { get; init; }
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

    // Null when the guard is reacting to standing reputation rather than a specific offence.
    public Guid? TriggeringCrimeId { get; init; }
}

public class TheftEncounter : Encounter
{
    public Guid TheftCrimeId { get; init; }
    public Guid ConfrontingCreatureId { get; init; }
    public string ConfrontingName { get; init; } = "";
    public Guid? SourceOwnerId { get; init; }
    public OwnerType? SourceOwnerType { get; init; }
    public List<Guid> ItemIds { get; init; } = [];
    public List<string> ItemNames { get; init; } = [];
    public List<TheftEncounterItem> ItemSelections { get; init; } = [];
    public List<Guid> WitnessCreatureIds { get; init; } = [];

    // Set when the confrontation interrupted a journey, so fleeing continues it.
    public Guid? InterruptedDestinationLocationId { get; init; }
}

public record TheftEncounterItem(Guid ItemId, int Quantity);

public enum SuspicionCause
{
    Sneaking,
    CastingMagicInPublic,
}

public class SuspicionEncounter : Encounter
{
    public required Guid GuardCreatureId { get; init; }
    public required string GuardName { get; init; }
    public required Guid CityFactionId { get; init; }
    public required SuspicionCause Cause { get; init; }
}
