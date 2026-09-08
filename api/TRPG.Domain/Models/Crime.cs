namespace TRPG.Domain.Models;

public enum CrimeResolution
{
    Pending,
    Reported,
    Unreported,
}

public enum CrimeWitnessResolution
{
    Pending,
    Reported,
    Dead,
}

public enum CrimeWitnessKind
{
    Saw,
    Heard,
}

public abstract class Crime
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid LocationId { get; init; }
    public Guid PlayerId { get; init; }
    public CrimeResolution Resolution { get; set; } = CrimeResolution.Pending;
    public DateTime? ResolvedAt { get; set; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public Guid WorldId { get; init; }

    // Whose law was broken, from where it happened. Null in the wilderness, where no city has claim.
    public Guid? CityId { get; init; }

    // Distinct from Resolution: that tracks whether witnesses reported it, this whether it was answered for.
    public DateTime? SettledAt { get; set; }
}

public class KillCrime : Crime
{
    public Guid VictimId { get; init; }
    public string VictimName { get; init; } = "";

    // Captured at kill time; the victim's corpse and its faction rows may be gone before this resolves.
    public List<Guid> VictimFactionIds { get; init; } = [];
}

public class AssaultCrime : Crime
{
    public Guid VictimId { get; init; }
    public string VictimName { get; init; } = "";

    // Captured at the first strike; the victim may be killed later in the same fight.
    public List<Guid> VictimFactionIds { get; init; } = [];
}

public enum TheftCrimeOutcome
{
    Taken,
    Apologized,
    Fled,
}

public class TheftCrime : Crime
{
    public Guid? OwnerFactionId { get; init; }
    public Guid OwnerCreatureId { get; init; }
    public string OwnerName { get; init; } = "";
    public List<TheftCrimeItem> Items { get; init; } = [];
    public TheftCrimeOutcome? Outcome { get; set; }
    public Guid SourceOwnerId { get; init; }
    public OwnerType SourceOwnerType { get; init; }
}

public record TheftCrimeItem(string Name, int Quantity);

public enum LockpickingCrimeOutcome
{
    SettledWithGuard,
    ResistedArrest,
}

public class LockpickingCrime : Crime
{
    public Guid BuildingId { get; init; }
    public string BuildingName { get; init; } = "";

    // Every faction the break-in wronged: the owner if it has one, and always the city
    // whose law protects it, the way a killing snapshots its victim's factions.
    public List<Guid> OwnerFactionIds { get; init; } = [];
    public LockpickingCrimeOutcome? Outcome { get; set; }
}

// Escaping custody, which shares picking a lock with LockpickingCrime and nothing else: its own
// penalties, its own wording, and a confrontation that does not wait on standing reputation.
public class JailbreakCrime : Crime
{
    public Guid BuildingId { get; init; }
    public string BuildingName { get; init; } = "";

    // Every faction the break-in wronged: the owner if it has one, and always the city
    // whose law protects it, the way a killing snapshots its victim's factions.
    public List<Guid> OwnerFactionIds { get; init; } = [];
    public LockpickingCrimeOutcome? Outcome { get; set; }
}

public class TrespassingCrime : Crime
{
    public Guid BuildingId { get; init; }
    public string BuildingName { get; init; } = "";

    // Every faction the break-in wronged: the owner if it has one, and always the city
    // whose law protects it, the way a killing snapshots its victim's factions.
    public List<Guid> OwnerFactionIds { get; init; } = [];
}

public class CrimeWitness
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CrimeId { get; init; }
    public Guid CreatureId { get; init; }

    // Hearsay reaches a victim who was not there, so it can never be what reports the crime.
    public CrimeWitnessKind Kind { get; init; } = CrimeWitnessKind.Saw;
    public CrimeWitnessResolution Resolution { get; set; } = CrimeWitnessResolution.Pending;
    public DateTime? ResolvedAt { get; set; }
    public DateTime WitnessedAt { get; init; } = DateTime.UtcNow;
    public Guid WorldId { get; init; }
}
