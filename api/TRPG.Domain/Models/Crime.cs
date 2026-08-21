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
    Silenced,
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
