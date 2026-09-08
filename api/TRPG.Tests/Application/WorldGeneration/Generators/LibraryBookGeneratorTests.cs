using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class LibraryBookGeneratorTests
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private static readonly BookSubject[] Subjects =
    [
        new(BookSubjectType.Country, "The Meridian Dominion"),
        new(BookSubjectType.City, "Ravenhollow"),
        new(BookSubjectType.Profession, "Blacksmith"),
    ];

    [Fact]
    public void Generate_ShelvesBooksOnEveryReadingWorkstation()
    {
        // Arrange
        var shelf = MakeShelf(WorkstationType.Reading);

        // Act
        var result = LibraryBookGenerator.Generate([shelf], Subjects, WorldId, new Random(1));

        // Assert
        Assert.NotEmpty(result.Books);
        Assert.All(result.Books, book => Assert.Equal(shelf.Id, book.Ownership.OwnerId));
        Assert.All(
            result.Books,
            book => Assert.Equal(OwnerType.Workstation, book.Ownership.OwnerType)
        );
    }

    [Fact]
    public void Generate_IgnoresWorkstationsThatAreNotForReading()
    {
        // Arrange
        var forge = MakeShelf(WorkstationType.Trade);

        // Act
        var result = LibraryBookGenerator.Generate([forge], Subjects, WorldId, new Random(1));

        // Assert
        Assert.Empty(result.Books);
        Assert.Empty(result.Works);
    }

    [Fact]
    public void Generate_ReusesOneWorkPerTitle_SoTheTextIsOnlyEverWrittenOnce()
    {
        // Arrange — one subject and a fixed seed guarantees repeated titles across two shelves.
        var shelves = new Prop[]
        {
            MakeShelf(WorkstationType.Reading),
            MakeShelf(WorkstationType.Reading),
        };

        // Act
        var result = LibraryBookGenerator.Generate(
            shelves,
            [new BookSubject(BookSubjectType.City, "Ravenhollow")],
            WorldId,
            new Random(7)
        );

        // Assert
        var workIds = result.Books.Select(book => book.WorkId).Distinct().ToArray();
        Assert.Equal(result.Works.Count, workIds.Length);
        Assert.Equal(
            result.Works.Select(work => work.Title).Distinct().Count(),
            result.Works.Count
        );
    }

    [Fact]
    public void Generate_TitlesEveryBookAndGivesItPages()
    {
        // Arrange
        var shelf = MakeShelf(WorkstationType.Reading);

        // Act
        var result = LibraryBookGenerator.Generate([shelf], Subjects, WorldId, new Random(3));

        // Assert — a shelf listing needs a title and a page count before anything is ever read.
        Assert.All(result.Works, work => Assert.NotEmpty(work.Title));
        Assert.All(result.Works, work => Assert.True(work.PageCount > 0));
        Assert.All(result.Works, work => Assert.Equal(BookTier.Flavour, work.Tier));
    }

    private static Workstation MakeShelf(WorkstationType workstationType) =>
        new()
        {
            LocationId = Guid.NewGuid(),
            WorldId = WorldId,
            Name = "Bookcase",
            Description = "A tall bookcase filled with tomes.",
            WorkstationType = workstationType,
        };
}
