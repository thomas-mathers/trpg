namespace TRPG.Data.Models;

public enum KnowledgeSubjectType
{
    Country,
    City,
    Faction,
    Creature,
}

public class CreatureKnowledge
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid KnowerId { get; init; }
    public Guid SubjectId { get; init; }
    public KnowledgeSubjectType SubjectType { get; init; }
    public Guid WorldId { get; init; }
}
