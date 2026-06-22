namespace TRPG.Models;

internal class Province {
    public Circle Boundary { get; init; } = null!;
    public Guid CountryId { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
}