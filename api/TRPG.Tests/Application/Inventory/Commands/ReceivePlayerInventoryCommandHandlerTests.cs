using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Quests;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class ReceivePlayerInventoryCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ReceivePlayerInventoryCommandHandler _receiveHandler = null!;
    private readonly Creature _fromCreature = Builders.MakeCreature(
        WorldId,
        locationId: LocationId
    );
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);
    private readonly Container _container = Builders.MakeContainer(WorldId, LocationId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _receiveHandler =
            _serviceProvider.GetRequiredService<ReceivePlayerInventoryCommandHandler>();

        _context.Creatures.AddRange(_fromCreature, _player);
        _context.Props.Add(_container);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Item> SeedItemOnFromCreature(int quantity)
    {
        var item = Builders.MakeWeaponItem(WorldId);
        item.Quantity = quantity;
        item.Ownership.OwnerId = _fromCreature.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    private async Task<CreatureQuestObjective> SeedCollectItemObjective(Guid itemId)
    {
        var quest = Builders.MakeQuest(_fromCreature.Id, WorldId);
        var objective = new CollectItemObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            ItemId = itemId,
        };
        var progress = new CreatureQuestObjective
        {
            CreatureId = _player.Id,
            ObjectiveId = objective.Id,
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
        return progress;
    }

    private async Task<Gold> SeedGoldOnFromCreature(int quantity)
    {
        var gold = Builders.MakeGold(WorldId, quantity);
        gold.Ownership.OwnerId = _fromCreature.Id;
        gold.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(gold);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return gold;
    }

    private static async Task<int> GetGoldQuantity(TrpgDbContext context, Guid ownerId) =>
        await context
            .Items.OfType<Gold>()
            .Where(i => i.Ownership.OwnerId == ownerId)
            .Select(i => (int?)i.Quantity)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken)
        ?? 0;

    private async Task<Item> SeedAmmunitionOnFromCreature(int quantity)
    {
        var item = Builders.MakeAmmunitionItem(WorldId);
        item.Quantity = quantity;
        item.Ownership.OwnerId = _fromCreature.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    private async Task<Item> SeedItemOnContainer(int quantity)
    {
        var item = Builders.MakeWeaponItem(WorldId);
        item.Quantity = quantity;
        item.Ownership.OwnerId = _container.Id;
        item.Ownership.OwnerType = OwnerType.Container;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    [Fact]
    public async Task Handle_MovesGold_WhenGoldItemIsSelected()
    {
        // Arrange
        var gold = await SeedGoldOnFromCreature(100);

        // Act
        await _receiveHandler.Handle(
            new ReceivePlayerInventoryCommand
            {
                From = new ItemOwnerReference(_fromCreature.Id, OwnerType.Creature),
                Items = [new ItemSelection(gold.Id, 100)],
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.Equal(0, await GetGoldQuantity(verifyContext, _fromCreature.Id));
        Assert.Equal(100, await GetGoldQuantity(verifyContext, _player.Id));
    }

    [Fact]
    public async Task Handle_LeavesGoldUntouched_WhenGoldIsNotSelected()
    {
        // Arrange
        await SeedGoldOnFromCreature(100);
        var item = await SeedItemOnFromCreature(quantity: 1);

        // Act
        await _receiveHandler.Handle(
            new ReceivePlayerInventoryCommand
            {
                From = new ItemOwnerReference(_fromCreature.Id, OwnerType.Creature),
                Items = [new ItemSelection(item.Id, 1)],
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.Equal(100, await GetGoldQuantity(verifyContext, _fromCreature.Id));
    }

    [Fact]
    public async Task Handle_MovesSelectedItems_FromCorpseToPlayer()
    {
        // Arrange
        var item = await SeedItemOnFromCreature(quantity: 3);

        // Act
        await _receiveHandler.Handle(
            new ReceivePlayerInventoryCommand
            {
                From = new ItemOwnerReference(_fromCreature.Id, OwnerType.Creature),
                Items = [new ItemSelection(item.Id, 3)],
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var movedItem = await verifyContext.Items.SingleAsync(
            i => i.Id == item.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_player.Id, movedItem.Ownership.OwnerId);
        Assert.Equal(3, movedItem.Quantity);
    }

    [Fact]
    public async Task Handle_UpdatesCollectObjectiveAndPublishesJournalEvent_WhenPlayerReceivesQuestItem()
    {
        // Arrange
        var item = await SeedItemOnFromCreature(quantity: 1);
        var progress = await SeedCollectItemObjective(item.Id);

        // Act
        await _receiveHandler.Handle(
            new ReceivePlayerInventoryCommand
            {
                From = new ItemOwnerReference(_fromCreature.Id, OwnerType.Creature),
                Items = [new ItemSelection(item.Id, 1)],
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(1, progress.Amount);
        var gameEvents = _serviceProvider.GetRequiredService<TestGameClientEventPublisher>();
        Assert.Collection(
            gameEvents.PublishedEvents,
            gameEvent => Assert.IsType<QuestObjectiveCompletedEvent>(gameEvent),
            gameEvent => Assert.IsType<QuestJournalUpdatedEvent>(gameEvent)
        );
    }

    [Fact]
    public async Task Handle_MovesBothGoldAndItems_WhenBothAreSelected()
    {
        // Arrange
        var gold = await SeedGoldOnFromCreature(100);
        var item = await SeedItemOnFromCreature(quantity: 1);

        // Act
        await _receiveHandler.Handle(
            new ReceivePlayerInventoryCommand
            {
                From = new ItemOwnerReference(_fromCreature.Id, OwnerType.Creature),
                Items = [new ItemSelection(gold.Id, 100), new ItemSelection(item.Id, 1)],
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var movedItem = await verifyContext.Items.SingleAsync(
            i => i.Id == item.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(100, await GetGoldQuantity(verifyContext, _player.Id));
        Assert.Equal(_player.Id, movedItem.Ownership.OwnerId);
    }

    [Fact]
    public async Task Handle_SplitsStack_WhenPartialQuantityIsSelected()
    {
        // Arrange
        var item = await SeedAmmunitionOnFromCreature(quantity: 10);

        // Act
        await _receiveHandler.Handle(
            new ReceivePlayerInventoryCommand
            {
                From = new ItemOwnerReference(_fromCreature.Id, OwnerType.Creature),
                Items = [new ItemSelection(item.Id, 4)],
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var remaining = await verifyContext.Items.SingleAsync(
            i => i.Id == item.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(6, remaining.Quantity);
        Assert.Equal(_fromCreature.Id, remaining.Ownership.OwnerId);

        var split = await verifyContext.Items.SingleAsync(
            i => i.Ownership.OwnerId == _player.Id && i.Id != item.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(4, split.Quantity);
        Assert.Equal(item.Name, split.Name);
    }

    [Fact]
    public async Task Handle_Throws_WhenPartiallyTransferringNonStackableItem()
    {
        // Arrange
        var item = await SeedItemOnFromCreature(quantity: 2);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _receiveHandler.Handle(
                new ReceivePlayerInventoryCommand
                {
                    From = new ItemOwnerReference(_fromCreature.Id, OwnerType.Creature),
                    Items = [new ItemSelection(item.Id, 1)],
                    PlayerId = _player.Id,
                    WorldId = WorldId,
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_TransfersPartialGold_WhenLessThanFullAmountIsSelected()
    {
        // Arrange
        var gold = await SeedGoldOnFromCreature(100);

        // Act
        await _receiveHandler.Handle(
            new ReceivePlayerInventoryCommand
            {
                From = new ItemOwnerReference(_fromCreature.Id, OwnerType.Creature),
                Items = [new ItemSelection(gold.Id, 30)],
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.Equal(70, await GetGoldQuantity(verifyContext, _fromCreature.Id));
        Assert.Equal(30, await GetGoldQuantity(verifyContext, _player.Id));
    }

    [Fact]
    public async Task Handle_Throws_WhenRequestingMoreThanIsAvailable()
    {
        // Arrange
        var item = await SeedItemOnFromCreature(quantity: 3);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _receiveHandler.Handle(
                new ReceivePlayerInventoryCommand
                {
                    From = new ItemOwnerReference(_fromCreature.Id, OwnerType.Creature),
                    Items = [new ItemSelection(item.Id, 5)],
                    PlayerId = _player.Id,
                    WorldId = WorldId,
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_Throws_WhenDuplicateSelectionsExceedAvailableQuantity()
    {
        // Arrange
        var item = await SeedAmmunitionOnFromCreature(quantity: 3);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _receiveHandler.Handle(
                new ReceivePlayerInventoryCommand
                {
                    From = new ItemOwnerReference(_fromCreature.Id, OwnerType.Creature),
                    Items = [new ItemSelection(item.Id, 2), new ItemSelection(item.Id, 2)],
                    PlayerId = _player.Id,
                    WorldId = WorldId,
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_MovesSelectedItems_FromContainerToPlayer()
    {
        // Arrange
        var item = await SeedItemOnContainer(quantity: 2);

        // Act
        await _receiveHandler.Handle(
            new ReceivePlayerInventoryCommand
            {
                From = new ItemOwnerReference(_container.Id, OwnerType.Container),
                Items = [new ItemSelection(item.Id, 2)],
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var movedItem = await verifyContext.Items.SingleAsync(
            i => i.Id == item.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_player.Id, movedItem.Ownership.OwnerId);
        Assert.Equal(OwnerType.Creature, movedItem.Ownership.OwnerType);
    }
}
