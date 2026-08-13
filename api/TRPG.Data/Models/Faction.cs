namespace TRPG.Data.Models;

public enum FactionTemperament
{
    Authoritative,
    Territorial,
    Predatory,
    Fanatical,
}

public class Faction
{
    public int Aggression { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public bool IsCityFaction { get; init; }
    public string Name { get; init; } = "";
    public int ReputationSensitivity { get; init; }
    public int RiskAversion { get; init; }
    public FactionTemperament Temperament { get; init; } = FactionTemperament.Authoritative;
    public Guid WorldId { get; init; }
}
