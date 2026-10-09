namespace TRPG.Domain.Models;

public class JourneyMember
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid JourneyId { get; init; }
    public Guid CreatureId { get; init; }
}
