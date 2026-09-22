namespace TRPG.Domain.Models;

public class GuardPatrolMember
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid RouteTravelerId { get; init; }
    public Guid CreatureId { get; init; }
}
