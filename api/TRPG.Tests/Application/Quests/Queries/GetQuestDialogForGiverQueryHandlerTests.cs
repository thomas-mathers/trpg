using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

public sealed class GetQuestDialogForGiverQueryHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetQuestDialogForGiverQueryHandler _handler = null!;
    private readonly Creature _giver = Builders.MakeCreature(WorldId);
    private readonly Creature _player = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetQuestDialogForGiverQueryHandler>();
        _context.Creatures.AddRange(_giver, _player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTurnInMode_WhenAQuestIsReadyToComplete()
    {
        // Arrange
        var available = Builders.MakeQuest(_giver.Id, WorldId);
        var ready = Builders.MakeQuest(_giver.Id, WorldId);
        _context.Quests.AddRange(available, ready);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = ready.Id,
                Status = QuestStatus.ReadyToComplete,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetQuestDialogForGiverQuery
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                GiverId = _giver.Id,
                QuestId = ready.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ready.Name, result.Quest.Name);
        Assert.Equal(QuestDialogMode.TurnIn, result.Mode);
    }

    [Fact]
    public async Task Handle_ReturnsOfferMode_WhenNoQuestIsReadyButOneIsAvailable()
    {
        // Arrange
        var available = Builders.MakeQuest(_giver.Id, WorldId);
        _context.Quests.Add(available);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetQuestDialogForGiverQuery
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                GiverId = _giver.Id,
                QuestId = available.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(available.Name, result.Quest.Name);
        Assert.Equal(QuestDialogMode.Offer, result.Mode);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheGiverHasNothingForThePlayer()
    {
        // Act
        var result = await _handler.Handle(
            new GetQuestDialogForGiverQuery
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                GiverId = _giver.Id,
                QuestId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }
}
