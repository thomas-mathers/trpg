namespace TRPG.Data.Models;

public enum RelationshipType
{
    Mother,
    Father,
    Sister,
    Brother,
    Son,
    Daughter,
    Husband,
    Wife,
}

public class Relationship
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SubjectId { get; init; }
    public Guid RelativeId { get; init; }
    public RelationshipType RelationshipType { get; init; }
    public Guid WorldId { get; init; }
}
