using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

[Collection("Database")]
public sealed class GetQuestInteractionsForGiverQueryHandlerTests(DatabaseFixture db)
    : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetQuestInteractionsForGiverQueryHandler _handler = null!;
    private readonly Creature _giver = Builders.MakeCreature(WorldId);
    private readonly Creature _player = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetQuestInteractionsForGiverQueryHandler>();
        _context.Creatures.AddRange(_giver, _player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOfferableQuestWithObjectives_WhenPrerequisitesAreCompleted()
    {
        // Arrange
        var prerequisite = Builders.MakeQuest(_giver.Id, WorldId);
        var available = Builders.MakeQuest(_giver.Id, WorldId);
        var locked = new Quest
        {
            WorldId = WorldId,
            GiverId = _giver.Id,
            Name = "Locked quest",
            Description = "A locked quest.",
            GoldReward = 100,
            PrerequisiteQuestIds = [prerequisite.Id, Guid.NewGuid()],
        };
        var objective = new CollectItemObjective
        {
            QuestId = available.Id,
            WorldId = WorldId,
            Name = "Recover the package",
            Description = "Recover the package.",
            ItemId = Guid.NewGuid(),
            RequiredAmount = 2,
        };
        _context.Quests.AddRange(prerequisite, available, locked);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = prerequisite.Id,
                Status = QuestStatus.Completed,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetQuestInteractionsForGiverQuery
            {
                GiverId = _giver.Id,
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var quest = Assert.Single(result.AvailableQuests);
        Assert.Equal(available.Name, quest.Name);
        var returnedObjective = Assert.Single(quest.Objectives);
        Assert.Equal(objective.Name, returnedObjective.Name);
        Assert.Equal(objective.RequiredAmount, returnedObjective.RequiredAmount);
        Assert.Empty(result.ReadyToCompleteQuests);
    }

    [Fact]
    public async Task Handle_ReturnsReadyQuest_WhenPlayerCanTurnItInToGiver()
    {
        // Arrange
        var readyQuest = Builders.MakeQuest(_giver.Id, WorldId);
        _context.Quests.Add(readyQuest);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = readyQuest.Id,
                Status = QuestStatus.ReadyToComplete,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetQuestInteractionsForGiverQuery
            {
                GiverId = _giver.Id,
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result.AvailableQuests);
        Assert.Equal(readyQuest.Name, Assert.Single(result.ReadyToCompleteQuests).Name);
    }

    [Fact]
    public async Task Handle_ReturnsActiveQuest_WhenPlayerHasAcceptedButNotFinishedIt()
    {
        // Arrange
        var acceptedQuest = Builders.MakeQuest(_giver.Id, WorldId);
        _context.Quests.Add(acceptedQuest);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = acceptedQuest.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetQuestInteractionsForGiverQuery
            {
                GiverId = _giver.Id,
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(acceptedQuest.Name, Assert.Single(result.ActiveQuests).Name);
        Assert.Empty(result.CompletedQuests);
    }

    [Fact]
    public async Task Handle_ReturnsCompletedQuest_WhenPlayerHasTurnedItIn()
    {
        // Arrange
        var completedQuest = Builders.MakeQuest(_giver.Id, WorldId);
        _context.Quests.Add(completedQuest);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = completedQuest.Id,
                Status = QuestStatus.Completed,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetQuestInteractionsForGiverQuery
            {
                GiverId = _giver.Id,
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(completedQuest.Name, Assert.Single(result.CompletedQuests).Name);
        Assert.Empty(result.ActiveQuests);
        Assert.Empty(result.AvailableQuests);
    }
}
