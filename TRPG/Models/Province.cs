namespace TRPG.Models;

internal class Province
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CountryId { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public Circle Boundary { get; init; } = null!;
}
