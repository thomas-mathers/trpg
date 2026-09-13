using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Books.EventHandlers;
using TRPG.Application.Common.Events;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Books.EventHandlers;

public sealed class ItemGivenToCreatureEventHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ItemGivenToCreatureEventHandler _handler = null!;
    private readonly Creature _giver = Builders.MakeCreature(WorldId);
    private readonly Creature _recipient = Builders.MakeCreature(WorldId);
    private readonly Secret _secret = new()
    {
        WorldId = WorldId,
        Subject = "the countersign of the Ashen Hand",
        Value = "ashes before dawn",
    };

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ItemGivenToCreatureEventHandler>();

        _context.Creatures.AddRange(_giver, _recipient);
        _context.Secrets.Add(_secret);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_TeachesTheRecipientTheSecret_WhenTheGivenItemIsABookThatRecordsIt()
    {
        // Arrange
        var work = new BookWork
        {
            WorldId = WorldId,
            Title = $"The Ledger of the Ashen Hand {Guid.NewGuid():N}",
            Tier = BookTier.Clue,
            SubjectType = BookSubjectType.Faction,
            SubjectName = "The Ashen Hand",
            PageCount = 1,
            SecretId = _secret.Id,
            SecretPageNumber = 1,
        };
        var book = Builders.MakeBook(work.Id, worldId: WorldId);
        book.Ownership.OwnerId = _recipient.Id;
        book.Ownership.OwnerType = OwnerType.Creature;
        _context.BookWorks.Add(work);
        _context.Items.Add(book);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ItemGivenToCreatureEvent(_giver.Id, WorldId, book.Id, _recipient.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(
            await _context.CreatureKnowledge.AnyAsync(
                knowledge =>
                    knowledge.KnowerId == _recipient.Id
                    && knowledge.SubjectId == _secret.Id
                    && knowledge.SubjectType == KnowledgeSubjectType.Secret,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenTheGivenItemIsNotABook()
    {
        // Arrange
        var item = Builders.MakeItem(WorldId);
        item.Ownership.OwnerId = _recipient.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ItemGivenToCreatureEvent(_giver.Id, WorldId, item.Id, _recipient.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(
            await _context.CreatureKnowledge.AnyAsync(
                knowledge => knowledge.KnowerId == _recipient.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenTheBookRecordsNoSecret()
    {
        // Arrange
        var work = new BookWork
        {
            WorldId = WorldId,
            Title = $"A History of Ravenhollow {Guid.NewGuid():N}",
            Tier = BookTier.Flavour,
            SubjectType = BookSubjectType.City,
            SubjectName = "Ravenhollow",
            PageCount = 1,
        };
        var book = Builders.MakeBook(work.Id, worldId: WorldId);
        book.Ownership.OwnerId = _recipient.Id;
        book.Ownership.OwnerType = OwnerType.Creature;
        _context.BookWorks.Add(work);
        _context.Items.Add(book);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ItemGivenToCreatureEvent(_giver.Id, WorldId, book.Id, _recipient.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(
            await _context.CreatureKnowledge.AnyAsync(
                knowledge => knowledge.KnowerId == _recipient.Id,
                TestContext.Current.CancellationToken
            )
        );
    }
}
