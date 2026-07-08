namespace TRPG.Models;

internal enum RelationshipType {
    Mother,
    Father,
    Sister,
    Brother,
    Son,
    Daughter
}

// SubjectId's RelationshipType is RelativeId — e.g. (Subject=kid, Relative=mom, Mother) reads "kid's mother is mom".
internal class Relationship {
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SubjectId { get; init; }
    public Guid RelativeId { get; init; }
    public RelationshipType RelationshipType { get; init; }
    public Guid WorldId { get; init; }
}
