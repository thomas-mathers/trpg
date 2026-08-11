using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

[Collection("Database")]
public sealed class GetQuestJournalQueryHandlerTests(DatabaseFixture db) : IAsyncLifetime
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
    }
}
