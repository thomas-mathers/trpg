using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Quests.Events;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests;

[Collection("Database")]
public sealed class CreatureKilledQuestGiverEventHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private readonly Creature _giver = Builders.MakeCreature(WorldId);
    private readonly Creature _player = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _context.Creatures.AddRange(_giver, _player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_FailsActiveQuestAndUnlocksItsItem_WhenQuestGiverIsKilled()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var quest = Builders.MakeQuest(_giver.Id, WorldId, name: "Recover the Ledger");
        var objective = new CollectItemObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            ItemId = itemId,
        };
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = quest.Id,
                Status = QuestStatus.ReadyToComplete,
                WorldId = WorldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            new CreatureQuestObjective
            {
                CreatureId = _player.Id,
                ObjectiveId = objective.Id,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = _serviceProvider.GetRequiredService<CreatureKilledQuestGiverEventHandler>();

        // Act
        await handler.Handle(
            new CreatureKilledEvent(_player.Id, WorldId, _giver.Id, _giver.CreatureType),
            TestContext.Current.CancellationToken
        );

        // Assert
        var creatureQuest = await _context.CreatureQuests.SingleAsync(
            creatureQuest =>
                creatureQuest.CreatureId == _player.Id && creatureQuest.QuestId == quest.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(QuestStatus.Failed, creatureQuest.Status);
        Assert.False(creatureQuest.IsTracked);

        var getActiveQuestItemIds = _serviceProvider.GetRequiredService<
            IQueryHandler<GetActiveQuestItemIdsQuery, IReadOnlyCollection<Guid>>
        >();
        var activeItemIds = await getActiveQuestItemIds.Handle(
            new GetActiveQuestItemIdsQuery { PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );
        Assert.DoesNotContain(itemId, activeItemIds);
        Assert.Contains(
            _serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents,
            gameEvent => gameEvent == new QuestJournalUpdatedEvent("Recover the Ledger")
        );
    }

    [Fact]
    public async Task Handle_LeavesCompletedQuestsUnchanged_WhenQuestGiverIsKilled()
    {
        // Arrange
        var quest = Builders.MakeQuest(_giver.Id, WorldId);
        _context.Quests.Add(quest);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = quest.Id,
                Status = QuestStatus.Completed,
                IsTracked = false,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = _serviceProvider.GetRequiredService<CreatureKilledQuestGiverEventHandler>();

        // Act
        await handler.Handle(
            new CreatureKilledEvent(_player.Id, WorldId, _giver.Id, _giver.CreatureType),
            TestContext.Current.CancellationToken
        );

        // Assert
        var creatureQuest = await _context.CreatureQuests.SingleAsync(
            creatureQuest =>
                creatureQuest.CreatureId == _player.Id && creatureQuest.QuestId == quest.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(QuestStatus.Completed, creatureQuest.Status);
        Assert.Empty(_serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents);
    }
}
