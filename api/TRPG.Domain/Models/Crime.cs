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

public abstract class Crime
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid LocationId { get; init; }
    public Guid PlayerId { get; init; }
    public CrimeResolution Resolution { get; set; } = CrimeResolution.Pending;
    public DateTime? ResolvedAt { get; set; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public Guid WorldId { get; init; }
}

public class KillCrime : Crime
{
    public Guid VictimId { get; init; }
    public string VictimName { get; init; } = "";

    // Captured at kill time; the victim's corpse and its faction rows may be gone before this resolves.
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
    public Guid? OwnerFactionId { get; init; }
    public LockpickingCrimeOutcome? Outcome { get; set; }
}

public class TrespassingCrime : Crime
{
    public Guid BuildingId { get; init; }
    public string BuildingName { get; init; } = "";
    public Guid? OwnerFactionId { get; init; }
}

public class CrimeWitness
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CrimeId { get; init; }
    public Guid CreatureId { get; init; }
    public CrimeWitnessResolution Resolution { get; set; } = CrimeWitnessResolution.Pending;
    public DateTime? ResolvedAt { get; set; }
    public DateTime WitnessedAt { get; init; } = DateTime.UtcNow;
    public Guid WorldId { get; init; }
}
