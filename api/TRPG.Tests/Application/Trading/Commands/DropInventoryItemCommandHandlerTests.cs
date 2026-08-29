using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Trading.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Trading.Commands;

[Collection("Database")]
public sealed class DropInventoryItemCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private DropInventoryItemCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<DropInventoryItemCommandHandler>();

        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DeletesItem_WhenDroppingEntireQuantity()
    {
        // Arrange
        var item = Builders.MakeWeapon(WorldId);
        item.Quantity = 1;
        item.Ownership.OwnerId = _player.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new DropInventoryItemCommand
            {
                PlayerId = _player.Id,
                ItemId = item.Id,
                Quantity = 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext.Items.AnyAsync(
                candidate => candidate.Id == item.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_ReducesQuantity_WhenDroppingPartOfAStack()
    {
        // Arrange
        var item = Builders.MakeConsumable(WorldId);
        item.Quantity = 3;
        item.Ownership.OwnerId = _player.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new DropInventoryItemCommand
            {
                PlayerId = _player.Id,
                ItemId = item.Id,
                Quantity = 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var remaining = await verifyContext.Items.SingleAsync(
            candidate => candidate.Id == item.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(2, remaining.Quantity);
    }

    [Fact]
    public async Task Handle_Throws_WhenQuantityIsNotPositive()
    {
        // Arrange
        var item = Builders.MakeWeapon(WorldId);
        item.Ownership.OwnerId = _player.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new DropInventoryItemCommand
                {
                    PlayerId = _player.Id,
                    ItemId = item.Id,
                    Quantity = 0,
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_Throws_WhenItemIsRequiredForAnActiveQuest()
    {
        // Arrange
        var item = Builders.MakeItem(WorldId);
        item.Ownership.OwnerId = _player.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        var quest = Builders.MakeQuest(Guid.NewGuid(), WorldId);
        var objective = new CollectItemObjective
        {
            QuestId = quest.Id,
            WorldId = WorldId,
            Name = "Recover item",
            Description = "Recover item",
            ItemId = item.Id,
        };
        _context.Items.Add(item);
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
                Objective = objective,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new DropInventoryItemCommand
                {
                    PlayerId = _player.Id,
                    ItemId = item.Id,
                    Quantity = 1,
                },
                TestContext.Current.CancellationToken
            )
        );
    }
}
