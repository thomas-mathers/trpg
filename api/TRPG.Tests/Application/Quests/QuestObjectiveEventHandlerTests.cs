using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Quests.EventHandlers;
using TRPG.Application.Quests.Events;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests;

public sealed class QuestObjectiveEventHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private const string GiveItemKindItemName = "Goblin Ear";

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
    [InlineData(ObjectiveKind.GiveItem)]
    [InlineData(ObjectiveKind.GiveItemKind)]
    [InlineData(ObjectiveKind.InteractWithProp)]
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

    [Fact]
    public async Task Handle_AdvancesClearLocationObjective_WhenTheKillIsInsideTheTargetBuilding()
    {
        // Arrange
        var building = Builders.MakeBuilding(worldId: WorldId);
        var roomId = Guid.NewGuid();
        var roomLocation = Builders.MakeLocation(WorldId, roomId: roomId);
        var room = Builders.MakeRoom(
            building.Id,
            id: roomId,
            worldId: WorldId,
            locationId: roomLocation.Id
        );
        var seeded = await SeedClearLocationObjective(building.Id);
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(roomLocation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _serviceProvider
            .GetRequiredService<CreatureKilledQuestEventHandler>()
            .Handle(
                new CreatureKilledEvent(
                    _player.Id,
                    WorldId,
                    Guid.NewGuid(),
                    CreatureType.Beast,
                    roomLocation.Id
                ),
                TestContext.Current.CancellationToken
            );

        // Assert
        var progress = await _context.CreatureQuestObjectives.SingleAsync(
            objective => objective.Id == seeded.Progress.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, progress.Amount);
    }

    [Fact]
    public async Task Handle_DoesNotAdvanceClearLocationObjective_WhenTheKillIsInADifferentBuilding()
    {
        // Arrange
        var targetBuilding = Builders.MakeBuilding(worldId: WorldId);
        var otherBuilding = Builders.MakeBuilding(worldId: WorldId);
        var otherRoomId = Guid.NewGuid();
        var otherRoomLocation = Builders.MakeLocation(WorldId, roomId: otherRoomId);
        var otherRoom = Builders.MakeRoom(
            otherBuilding.Id,
            id: otherRoomId,
            worldId: WorldId,
            locationId: otherRoomLocation.Id
        );
        var seeded = await SeedClearLocationObjective(targetBuilding.Id);
        _context.Buildings.AddRange(targetBuilding, otherBuilding);
        _context.Rooms.Add(otherRoom);
        _context.Locations.Add(otherRoomLocation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _serviceProvider
            .GetRequiredService<CreatureKilledQuestEventHandler>()
            .Handle(
                new CreatureKilledEvent(
                    _player.Id,
                    WorldId,
                    Guid.NewGuid(),
                    CreatureType.Beast,
                    otherRoomLocation.Id
                ),
                TestContext.Current.CancellationToken
            );

        // Assert
        var progress = await _context.CreatureQuestObjectives.SingleAsync(
            objective => objective.Id == seeded.Progress.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(0, progress.Amount);
    }

    private async Task<SeededObjective> SeedClearLocationObjective(Guid buildingId)
    {
        var quest = Builders.MakeQuest(_giver.Id, WorldId);
        var objective = new ClearLocationObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            BuildingId = buildingId,
            RequiredAmount = 2,
        };
        var progress = new CreatureQuestObjective
        {
            CreatureId = _player.Id,
            ObjectiveId = objective.Id,
            Amount = 0,
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

        return new SeededObjective(progress, creatureQuest, _ => Task.CompletedTask);
    }

    private async Task<SeededObjective> SeedObjective(ObjectiveKind kind, int amount = 0)
    {
        var quest = Builders.MakeQuest(_giver.Id, WorldId);
        var targetId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var objective = MakeObjective(kind, quest.Id, targetId, recipientId, locationId);
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
        if (kind == ObjectiveKind.GiveItemKind)
        {
            _context.Items.Add(
                new Item
                {
                    Id = targetId,
                    WorldId = WorldId,
                    Name = GiveItemKindItemName,
                    Description = "A test item",
                    Weight = 1,
                }
            );
        }
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new SeededObjective(
            progress,
            creatureQuest,
            MakeAct(kind, targetId, recipientId, locationId)
        );
    }

    private QuestObjective MakeObjective(
        ObjectiveKind kind,
        Guid questId,
        Guid targetId,
        Guid recipientId,
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
            ObjectiveKind.GiveItem => new GiveItemsObjective
            {
                WorldId = WorldId,
                QuestId = questId,
                ItemIds = [targetId],
                RecipientId = recipientId,
            },
            ObjectiveKind.GiveItemKind => new GiveItemKindObjective
            {
                WorldId = WorldId,
                QuestId = questId,
                ItemName = GiveItemKindItemName,
                RecipientId = recipientId,
            },
            ObjectiveKind.InteractWithProp => new InteractWithPropObjective
            {
                WorldId = WorldId,
                QuestId = questId,
                TriggerId = targetId,
            },
            _ => throw new InvalidOperationException(),
        };

    private Func<CancellationToken, Task> MakeAct(
        ObjectiveKind kind,
        Guid targetId,
        Guid recipientId,
        Guid locationId
    ) =>
        kind switch
        {
            ObjectiveKind.KillCreature => cancellationToken =>
                _serviceProvider
                    .GetRequiredService<CreatureKilledQuestEventHandler>()
                    .Handle(
                        new CreatureKilledEvent(
                            _player.Id,
                            WorldId,
                            targetId,
                            CreatureType.Beast,
                            locationId
                        ),
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
                            CreatureType.Beast,
                            locationId
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
                        new PlayerMovedEvent(
                            _player.Id,
                            WorldId,
                            Guid.NewGuid(),
                            locationId,
                            GameClock.Epoch
                        ),
                        cancellationToken
                    ),
            ObjectiveKind.GiveItem => cancellationToken =>
                _serviceProvider
                    .GetRequiredService<ItemAcquiredQuestEventHandler>()
                    .Handle(
                        new ItemAcquiredEvent(_player.Id, WorldId, targetId),
                        cancellationToken
                    ),
            ObjectiveKind.GiveItemKind => cancellationToken =>
                _serviceProvider
                    .GetRequiredService<ItemAcquiredQuestEventHandler>()
                    .Handle(
                        new ItemAcquiredEvent(_player.Id, WorldId, targetId),
                        cancellationToken
                    ),
            ObjectiveKind.InteractWithProp => cancellationToken =>
                _serviceProvider
                    .GetRequiredService<TriggerActivatedQuestEventHandler>()
                    .Handle(
                        new TriggerActivatedEvent(_player.Id, WorldId, targetId),
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
        GiveItem,
        GiveItemKind,
        InteractWithProp,
    }

    private sealed record SeededObjective(
        CreatureQuestObjective Progress,
        CreatureQuest CreatureQuest,
        Func<CancellationToken, Task> Act
    );
}
