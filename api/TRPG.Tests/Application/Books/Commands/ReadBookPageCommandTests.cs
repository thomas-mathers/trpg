using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Books;
using TRPG.Application.Books.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Books.Commands;

[Collection("Database")]
public sealed class ReadBookPageCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<ReadBookPageCommand, ReadBookPageResult> _handler = null!;
    private readonly Creature _reader = Builders.MakeCreature(WorldId);
    private readonly BookWork _work = new()
    {
        WorldId = WorldId,
        Title = $"A History of Ravenhollow {Guid.NewGuid():N}",
        Tier = BookTier.Flavour,
        SubjectType = BookSubjectType.City,
        SubjectName = "Ravenhollow",
        PageCount = 3,
    };

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton(new BookPageComposer(new FakeChatClient()))
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<
            ICommandHandler<ReadBookPageCommand, ReadBookPageResult>
        >();

        _context.Creatures.Add(_reader);
        _context.BookWorks.Add(_work);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ComposesAndPersistsThePage_WhenItHasNeverBeenRead()
    {
        // Arrange

        // Act
        var result = await _handler.Handle(MakeCommand(1), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(result.Text);
        Assert.Equal(_work.Title, result.Title);
        Assert.Equal(3, result.PageCount);

        await using var verifyContext = db.CreateContext();
        var stored = await verifyContext.BookPages.SingleAsync(
            page => page.WorkId == _work.Id && page.PageNumber == 1,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(result.Text, stored.Text);
    }

    [Fact]
    public async Task Handle_ReturnsTheStoredText_SoASecondReadingMatchesTheFirst()
    {
        // Arrange
        var first = await _handler.Handle(MakeCommand(2), TestContext.Current.CancellationToken);

        // Act
        var second = await _handler.Handle(MakeCommand(2), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(first.Text, second.Text);

        await using var verifyContext = db.CreateContext();
        Assert.Single(
            await verifyContext
                .BookPages.Where(page => page.WorkId == _work.Id && page.PageNumber == 2)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_Throws_WhenThePageIsPastTheEndOfTheBook()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(MakeCommand(4), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_TeachesTheSecret_WhenTheReaderReachesThePageThatRecordsIt()
    {
        // Arrange
        var work = await SeedWorkWithSecretOnPage(2);

        // Act
        var result = await _handler.Handle(
            MakeCommand(2, work.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.RevealedSecret);

        await using var verifyContext = db.CreateContext();
        Assert.True(
            await verifyContext.CreatureKnowledge.AnyAsync(
                knowledge =>
                    knowledge.KnowerId == _reader.Id
                    && knowledge.SubjectId == work.SecretId
                    && knowledge.SubjectType == KnowledgeSubjectType.Secret,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_TeachesNothing_WhenTheReaderStopsBeforeTheSecretPage()
    {
        // Arrange
        var work = await SeedWorkWithSecretOnPage(2);

        // Act
        var result = await _handler.Handle(
            MakeCommand(1, work.Id),
            TestContext.Current.CancellationToken
        );

        // Assert — skimming the first page of a ledger teaches you nothing on the second.
        Assert.False(result.RevealedSecret);

        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext.CreatureKnowledge.AnyAsync(
                knowledge => knowledge.SubjectId == work.SecretId,
                TestContext.Current.CancellationToken
            )
        );
    }

    private ReadBookPageCommand MakeCommand(int pageNumber, Guid? workId = null) =>
        new()
        {
            WorldId = WorldId,
            ReaderId = _reader.Id,
            WorkId = workId ?? _work.Id,
            PageNumber = pageNumber,
        };

    private async Task<BookWork> SeedWorkWithSecretOnPage(int pageNumber)
    {
        var secret = new Secret
        {
            WorldId = WorldId,
            Subject = "the countersign of the Ashen Hand",
            Value = "ashes before dawn",
        };
        var work = new BookWork
        {
            WorldId = WorldId,
            Title = $"The Ledger of the Ashen Hand {Guid.NewGuid():N}",
            Tier = BookTier.Clue,
            SubjectType = BookSubjectType.Faction,
            SubjectName = "The Ashen Hand",
            PageCount = 3,
            SecretId = secret.Id,
            SecretPageNumber = pageNumber,
        };

        _context.Secrets.Add(secret);
        _context.BookWorks.Add(work);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return work;
    }
}
