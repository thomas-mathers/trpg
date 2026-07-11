namespace TRPG.Data.Models;

public enum KnowledgeSubjectType
{
    Country,
    City,
    Faction,
    Creature,
}

// A knower is aware a subject exists and can recall what they know of it. Populated during world
// generation from static facts (residence, family, factions); gameplay can add rows as creatures
// learn of new people and places.
public class CreatureKnowledge
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid KnowerId { get; init; }
    public Guid SubjectId { get; init; }
    public KnowledgeSubjectType SubjectType { get; init; }
    public Guid WorldId { get; init; }
}
