using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public record LibraryBookGeneratorResult(
    IReadOnlyCollection<BookWork> Works,
    IReadOnlyCollection<Book> Books
);

public static class LibraryBookGenerator
{
    private const int MinimumBooksPerShelf = 4;
    private const int MaximumBooksPerShelf = 7;
    private const int BookWeight = 2;
    private const int BookGoldValue = 12;

    public static LibraryBookGeneratorResult Generate(
        IReadOnlyCollection<Prop> props,
        IReadOnlyCollection<BookSubject> subjects,
        Guid worldId,
        Random random
    )
    {
        var shelves = props
            .OfType<Workstation>()
            .Where(workstation => workstation.WorkstationType == WorkstationType.Reading)
            .ToArray();
        if (shelves.Length == 0 || subjects.Count == 0)
        {
            return new LibraryBookGeneratorResult([], []);
        }

        var subjectList = subjects.ToArray();
        var worksByTitle = new Dictionary<string, BookWork>(StringComparer.OrdinalIgnoreCase);
        var books = new List<Book>();

        foreach (var shelf in shelves)
        {
            var count = random.Next(MinimumBooksPerShelf, MaximumBooksPerShelf + 1);
            for (var i = 0; i < count; i++)
            {
                var subject = subjectList[random.Next(subjectList.Length)];
                var generated = BookTitleGenerator.Generate(subject, random);
                var work = FindOrCreateWork(worksByTitle, generated, worldId);
                books.Add(Shelve(work, shelf.Id, worldId));
            }
        }

        return new LibraryBookGeneratorResult(worksByTitle.Values.ToArray(), books);
    }

    // Two shelves holding the same title hold the same work, so the text is only ever written once.
    private static BookWork FindOrCreateWork(
        Dictionary<string, BookWork> worksByTitle,
        GeneratedBookTitle generated,
        Guid worldId
    )
    {
        if (worksByTitle.TryGetValue(generated.Title, out var existing))
        {
            return existing;
        }

        var work = new BookWork
        {
            WorldId = worldId,
            Title = generated.Title,
            Tier = BookTier.Flavour,
            SubjectType = generated.Subject.Type,
            SubjectName = generated.Subject.Name,
            PageCount = generated.PageCount,
        };
        worksByTitle[generated.Title] = work;
        return work;
    }

    private static Book Shelve(BookWork work, Guid shelfId, Guid worldId) =>
        new()
        {
            WorldId = worldId,
            WorkId = work.Id,
            Name = work.Title,
            Description = "A bound volume.",
            Weight = BookWeight,
            Quantity = 1,
            GoldValue = BookGoldValue,
            Ownership = new ItemOwnership { OwnerId = shelfId, OwnerType = OwnerType.Workstation },
        };
}
