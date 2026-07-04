namespace TRPG.Models;

internal class City {
    public Guid CountryId { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public bool IsCapital { get; init; }
    public string Name { get; init; } = "";
    public Guid StateId { get; init; }
    public Guid WorldId { get; init; }
}