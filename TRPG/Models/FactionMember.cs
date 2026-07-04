namespace TRPG.Models;

internal enum FactionRole {
    Leader,
    Member
}

internal class FactionMember {
    public Guid FactionId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; init; }
    public FactionRole Role { get; set; }
    public Guid WorldId { get; init; }
}