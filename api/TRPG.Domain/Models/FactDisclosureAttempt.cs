namespace TRPG.Domain.Models;

public class FactDisclosureAttempt
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid PlayerId { get; init; }
    public Guid NpcId { get; init; }
    public Guid FactId { get; init; }
}
