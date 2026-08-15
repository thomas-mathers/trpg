using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Quests;
using TRPG.Application.Quests.Events;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests;

[Collection("Database")]
public sealed class QuestObjectiveEventHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Creature _giver = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _context.Creatures.AddRange(_player, _giver);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Theory]
    [InlineData(ObjectiveKind.KillCreature)]
    [InlineData(ObjectiveKind.KillCreatureType)]
    [InlineData(ObjectiveKind.CollectItem)]
    [InlineData(ObjectiveKind.ExploreLocation)]
    [InlineData(ObjectiveKind.SpeakToCreature)]
    public async Task Handle_AdvancesAndMarksReady_WhenEventMatchesObjective(ObjectiveKind kind)
    {
        // Arrange
        var seeded = await SeedObjective(kind);

        // Act
        await seeded.Act(TestContext.Current.CancellationToken);

        // Assert
        var progress = await _context.CreatureQuestObjectives.SingleAsync(
            objective => objective.Id == seeded.Progress.Id,
            TestContext.Current.CancellationToken
        );
        var quest = await _context.CreatureQuests.SingleAsync(
            creatureQuest => creatureQuest.Id == seeded.CreatureQuest.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, progress.Amount);
        Assert.Equal(QuestStatus.ReadyToComplete, quest.Status);
        var gameEvents = _serviceProvider.GetRequiredService<TestGameClientEventSink>();
        Assert.Collection(
            gameEvents.EnqueuedEvents,
            gameEvent => Assert.IsType<QuestObjectiveCompletedEvent>(gameEvent),
            gameEvent => Assert.IsType<QuestJournalUpdatedEvent>(gameEvent)
        );
    }

    [Fact]
    public async Task Handle_DoesNotAdvance_WhenObjectiveIsAlreadyComplete()
    {
        // Arrange
        var seeded = await SeedObjective(ObjectiveKind.CollectItem, amount: 1);

        // Act
        await seeded.Act(TestContext.Current.CancellationToken);

        // Assert
        var progress = await _context.CreatureQuestObjectives.SingleAsync(
            objective => objective.Id == seeded.Progress.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, progress.Amount);
        var gameEvents = _serviceProvider.GetRequiredService<TestGameClientEventSink>();
        Assert.Empty(gameEvents.EnqueuedEvents);
    }

    private async Task<SeededObjective> SeedObjective(ObjectiveKind kind, int amount = 0)
    {
        var quest = Builders.MakeQuest(_giver.Id, WorldId);
        var targetId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var objective = MakeObjective(kind, quest.Id, targetId, locationId);
        var progress = new CreatureQuestObjective
        {
            CreatureId = _player.Id,
            ObjectiveId = objective.Id,
            Amount = amount,
            WorldId = WorldId,
        };
        var creatureQuest = new CreatureQuest
        {
            CreatureId = _player.Id,
            QuestId = quest.Id,
            Status = QuestStatus.Accepted,
            WorldId = WorldId,
        };

        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuestObjectives.Add(progress);
        _context.CreatureQuests.Add(creatureQuest);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new SeededObjective(progress, creatureQuest, MakeAct(kind, targetId, locationId));
    }

    private QuestObjective MakeObjective(
        ObjectiveKind kind,
        Guid questId,
        Guid targetId,
        Guid locationId
    ) =>
        kind switch
        {
            ObjectiveKind.KillCreature => new KillCreatureObjective
            {
                WorldId = WorldId,
                QuestId = questId,
                CreatureId = targetId,
            },
            ObjectiveKind.KillCreatureType => new KillCreatureTypeObjective
            {
                WorldId = WorldId,
                QuestId = questId,
                CreatureType = CreatureType.Beast,
            },
            ObjectiveKind.CollectItem => new CollectItemObjective
            {
                WorldId = WorldId,
                QuestId = questId,
                ItemId = targetId,
            },
            ObjectiveKind.ExploreLocation => new ExploreLocationObjective
            {
                WorldId = WorldId,
                QuestId = questId,
                LocationId = locationId,
            },
            ObjectiveKind.SpeakToCreature => new SpeakToCreatureObjective
            {
                WorldId = WorldId,
                QuestId = questId,
                CreatureId = targetId,
            },
            _ => throw new InvalidOperationException(),
        };

    private Func<CancellationToken, Task> MakeAct(
        ObjectiveKind kind,
        Guid targetId,
        Guid locationId
    ) =>
        kind switch
        {
            ObjectiveKind.KillCreature => cancellationToken =>
                _serviceProvider
                    .GetRequiredService<CreatureKilledQuestEventHandler>()
                    .Handle(
                        new CreatureKilledEvent(_player.Id, WorldId, targetId, CreatureType.Beast),
                        cancellationToken
                    ),
            ObjectiveKind.KillCreatureType => cancellationToken =>
                _serviceProvider
                    .GetRequiredService<CreatureKilledQuestEventHandler>()
                    .Handle(
                        new CreatureKilledEvent(
                            _player.Id,
                            WorldId,
                            Guid.NewGuid(),
                            CreatureType.Beast
                        ),
                        cancellationToken
                    ),
            ObjectiveKind.CollectItem => cancellationToken =>
                _serviceProvider
                    .GetRequiredService<ItemAcquiredQuestEventHandler>()
                    .Handle(
                        new ItemAcquiredEvent(_player.Id, WorldId, targetId),
                        cancellationToken
                    ),
            ObjectiveKind.ExploreLocation => cancellationToken =>
                _serviceProvider
                    .GetRequiredService<PlayerMovedQuestEventHandler>()
                    .Handle(
                        new PlayerMovedEvent(_player.Id, WorldId, locationId),
                        cancellationToken
                    ),
            ObjectiveKind.SpeakToCreature => cancellationToken =>
                _serviceProvider
                    .GetRequiredService<ConversationStartedQuestEventHandler>()
                    .Handle(
                        new NpcConversationStartedEvent(_player.Id, WorldId, targetId),
                        cancellationToken
                    ),
            _ => throw new InvalidOperationException(),
        };

    public enum ObjectiveKind
    {
        KillCreature,
        KillCreatureType,
        CollectItem,
        ExploreLocation,
        SpeakToCreature,
    }

    private sealed record SeededObjective(
        CreatureQuestObjective Progress,
        CreatureQuest CreatureQuest,
        Func<CancellationToken, Task> Act
    );
}
