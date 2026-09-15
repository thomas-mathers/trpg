using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

public sealed class GetQuestJournalQueryHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private GetQuestJournalQueryHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Quest _quest = Builders.MakeQuest(Guid.NewGuid(), WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<GetQuestJournalQueryHandler>();
        var objective = new CollectItemObjective
        {
            WorldId = WorldId,
            QuestId = _quest.Id,
            Name = "Collect",
            Description = "Collect",
            ItemId = Guid.NewGuid(),
            RequiredAmount = 3,
        };
        _context.Creatures.Add(_player);
        _context.Quests.Add(_quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = _quest.Id,
                Status = QuestStatus.Accepted,
                IsTracked = false,
                WorldId = WorldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            new CreatureQuestObjective
            {
                CreatureId = _player.Id,
                ObjectiveId = objective.Id,
                Amount = 2,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsQuestTrackingAndObjectiveProgress()
    {
        // Arrange
        var query = new GetQuestJournalQuery { PlayerId = _player.Id, WorldId = WorldId };

        // Act
        var journal = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var quest = Assert.Single(journal);
        var objective = Assert.Single(quest.Objectives);
        Assert.False(quest.IsTracked);
        Assert.Equal(2, objective.Amount);
        Assert.Equal(3, objective.RequiredAmount);
        Assert.Null(objective.Items);
    }

    [Fact]
    public async Task Handle_BreaksDownGiveItemsObjectiveProgress_ByItemName()
    {
        // Arrange
        var firstGear = Builders.MakeItem(WorldId, "Construct Gear");
        firstGear.Ownership.OwnerId = _player.Id;
        firstGear.Ownership.OwnerType = OwnerType.Creature;
        var secondGear = Builders.MakeItem(WorldId, "Construct Gear");
        secondGear.Ownership.OwnerId = _player.Id;
        secondGear.Ownership.OwnerType = OwnerType.Creature;
        var goblinEar = Builders.MakeItem(WorldId, "Goblin Ear");
        goblinEar.Ownership.OwnerId = Guid.NewGuid();
        goblinEar.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.AddRange(firstGear, secondGear, goblinEar);

        var quest = Builders.MakeQuest(Guid.NewGuid(), WorldId);
        var objective = new GiveItemsObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            Name = "Gather materials",
            Description = "Gather materials",
            ItemIds = [firstGear.Id, secondGear.Id, goblinEar.Id],
            RecipientId = Guid.NewGuid(),
            RequiredAmount = 3,
        };
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            new CreatureQuestObjective
            {
                CreatureId = _player.Id,
                ObjectiveId = objective.Id,
                Amount = 2,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var journal = await _handler.Handle(
            new GetQuestJournalQuery { PlayerId = _player.Id, WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var journalEntry = Assert.Single(journal, entry => entry.Id == quest.Id);
        var progress = Assert.Single(journalEntry.Objectives);
        Assert.NotNull(progress.Items);
        var gearProgress = Assert.Single(progress.Items, item => item.Name == "Construct Gear");
        Assert.Equal(2, gearProgress.Amount);
        Assert.Equal(2, gearProgress.RequiredAmount);
        var earProgress = Assert.Single(progress.Items, item => item.Name == "Goblin Ear");
        Assert.Equal(0, earProgress.Amount);
        Assert.Equal(1, earProgress.RequiredAmount);
    }
}
