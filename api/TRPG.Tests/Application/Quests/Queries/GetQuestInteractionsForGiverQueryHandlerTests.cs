using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

public sealed class GetQuestInteractionsForGiverQueryHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
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
    public async Task Handle_ReturnsItemNames_WhenObjectiveIsGiveItemsObjective()
    {
        // Arrange
        var quest = Builders.MakeQuest(_giver.Id, WorldId);
        var itemOne = Builders.MakeItem(WorldId, name: "Lucan Ashvale's Pocket Watch");
        var itemTwo = Builders.MakeItem(WorldId, name: "The Crooked Chimney's Strongbox");
        var objective = new GiveItemsObjective
        {
            QuestId = quest.Id,
            WorldId = WorldId,
            Name = "Recover 2 items",
            Description = "Recover 2 items.",
            ItemIds = [itemOne.Id, itemTwo.Id],
            RecipientId = _giver.Id,
            RequiredAmount = 2,
        };
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.Items.AddRange(itemOne, itemTwo);
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
        var returnedObjective = Assert.Single(Assert.Single(result.AvailableQuests).Objectives);
        Assert.NotNull(returnedObjective.ItemNames);
        Assert.Equal(
            [itemOne.Name, itemTwo.Name],
            returnedObjective.ItemNames,
            StringComparer.Ordinal
        );
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

    [Fact]
    public async Task Handle_ExcludesAnUnrevealedQuest_FromAvailableQuests()
    {
        // Arrange
        var unrevealed = Builders.MakeQuest(_giver.Id, WorldId, requiredFactId: Guid.NewGuid());
        _context.Quests.Add(unrevealed);
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
    }

    [Fact]
    public async Task Handle_IncludesAQuestOnceRevealed_InAvailableQuests()
    {
        // Arrange
        var revealed = Builders.MakeQuest(_giver.Id, WorldId, requiredFactId: Guid.NewGuid());
        _context.CreatureKnowledge.Add(
            new CreatureKnowledge
            {
                WorldId = WorldId,
                KnowerId = _player.Id,
                SubjectId = revealed.RequiredFactId!.Value,
                SubjectType = KnowledgeSubjectType.Fact,
            }
        );
        _context.Quests.Add(revealed);
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
        Assert.Equal(revealed.Name, Assert.Single(result.AvailableQuests).Name);
    }
}
