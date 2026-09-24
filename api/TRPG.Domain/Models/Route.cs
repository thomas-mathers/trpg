namespace TRPG.Domain.Models;

public enum RouteTraversal
{
    Cyclic,
    Finite,
}

public class Route
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public required string Name { get; init; }
    public required RouteTraversal Traversal { get; init; }
}
