namespace TRPG.Domain.Models;

public class TravelNode
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid LocationId { get; init; }
    public required Point Position { get; init; }
    public Guid WorldId { get; init; }
}
