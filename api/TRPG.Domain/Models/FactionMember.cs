namespace TRPG.Domain.Models;

public enum FactionRole
{
    Leader,
    Member,
}

public class FactionMember
{
    public Guid CreatureId { get; init; }
    public Guid FactionId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public FactionRole Role { get; set; }
    public Guid WorldId { get; init; }
}
