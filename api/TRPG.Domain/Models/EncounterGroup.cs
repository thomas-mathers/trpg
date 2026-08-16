namespace TRPG.Domain.Models;

public class EncounterGroup
{
    public Guid FactionId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid LocationId { get; set; }
    public Guid WorldId { get; init; }
}

public class EncounterGroupMember
{
    public Guid CreatureId { get; init; }
    public Guid EncounterGroupId { get; init; }
    public Guid WorldId { get; init; }
}
