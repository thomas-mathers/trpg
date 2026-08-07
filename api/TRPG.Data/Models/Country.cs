namespace TRPG.Data.Models;

public enum CountryFocus
{
    Scientific,
    Political,
    Religious,
    Militaristic,
}

public class Country
{
    public Polygon Boundary { get; init; } = null!;
    public string Description { get; init; } = "";
    public CreatureType DominantRace { get; init; }
    public CountryFocus Focus { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid WorldId { get; init; }
}
