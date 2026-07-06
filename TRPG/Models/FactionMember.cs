namespace TRPG.Models;

internal enum FactionRole {
    Leader,
    Member
}

internal class FactionMember {
    public Guid CreatureId { get; init; }
    public Guid FactionId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public FactionRole Role { get; set; }
    public Guid WorldId { get; init; }
}