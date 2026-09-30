namespace TRPG.Domain.Models;

public enum BookTier
{
    Flavour,
    Clue,
}

public enum BookSubjectType
{
    Country,
    State,
    City,
    Faction,
    Profession,
    CreatureType,
    Building,
}

// The work, as opposed to the copy: two shelves holding the same title hold the same book, so the
// text is written once and every copy reads from it.
public class BookWork
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public string Title { get; init; } = "";
    public BookTier Tier { get; init; }

    // What the work is about, so a page can be composed without re-deriving it from the title.
    public BookSubjectType SubjectType { get; init; }
    public string SubjectName { get; init; } = "";

    public int PageCount { get; init; }

    public Guid? FactId { get; init; }
    public int? FactPageNumber { get; init; }
}

public class BookPage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid WorkId { get; init; }
    public int PageNumber { get; init; }
    public string Text { get; init; } = "";
}
