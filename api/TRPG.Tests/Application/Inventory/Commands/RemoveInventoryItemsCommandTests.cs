using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class RemoveInventoryItemsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetInventoryItemsByOwnerQueryHandler _getHandler = null!;
    private RemoveInventoryItemsCommandHandler _removeHandler = null!;
    private readonly Creature _creature = Builders.MakeCreature();
    private readonly Creature _otherCreature = Builders.MakeCreature();
    private readonly Item _item = Builders.MakeItem();
    private readonly Item _stackableItem = Builders.MakeConsumable();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _removeHandler = new RemoveInventoryItemsCommandHandler(_context);
        _getHandler = new GetInventoryItemsByOwnerQueryHandler(_context);

        _context.Creatures.AddRange(_creature, _otherCreature);
        _context.Items.AddRange(_item, _stackableItem);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task GiveToCreature(Item item, Guid creatureId, int quantity)
    {
        item.Quantity = quantity;
        item.Ownership.OwnerId = creatureId;
        item.Ownership.OwnerType = OwnerType.Creature;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_DecreasesQuantity()
    {
        // Arrange
        await GiveToCreature(_stackableItem, _creature.Id, 5);

        // Act
        await _removeHandler.Handle(
            new RemoveInventoryItemsCommand
            {
                Removals = [new InventoryItemRemoval(_creature.Id, _stackableItem.Id, 3)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(_creature.Id, OwnerType.Creature),
            },
            TestContext.Current.CancellationToken
        );
        Assert.Single(items);
        Assert.Equal(2, items[0].Quantity);
    }

    [Fact]
    public async Task Handle_RemovesEntry_WhenQuantityReachesZero()
    {
        // Arrange
        await GiveToCreature(_item, _creature.Id, 1);

        // Act
        await _removeHandler.Handle(
            new RemoveInventoryItemsCommand
            {
                Removals = [new InventoryItemRemoval(_creature.Id, _item.Id, 1)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(_creature.Id, OwnerType.Creature),
            },
            TestContext.Current.CancellationToken
        );
        Assert.Empty(items);
    }

    [Fact]
    public async Task Handle_Throws_WhenItemNotInInventory()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _removeHandler.Handle(
                new RemoveInventoryItemsCommand
                {
                    Removals = [new InventoryItemRemoval(_creature.Id, _item.Id, 1)],
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_ClampsCurrentHpDown_WhenRemovingEquippedItemReducesMaximum()
    {
        // Arrange
        var baseMaximumHp = _creature.MaximumHp;
        var gear = Builders.MakeArmor(
            worldId: _creature.WorldId,
            modifiers:
            [
                new AttributeModifier
                {
                    Attribute = AttributeName.MaximumHp,
                    Amount = 50,
                    AmountType = AmountType.Flat,
                },
            ]
        );
        _context.Items.Add(gear);
        await GiveToCreature(gear, _creature.Id, 1);
        await new EquipInventoryItemCommandHandler(_context).Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = gear.Id,
                Slot = EquipmentSlot.Chest,
            },
            TestContext.Current.CancellationToken
        );
        _creature.CurrentHp = _creature.MaximumHp;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _removeHandler.Handle(
            new RemoveInventoryItemsCommand
            {
                Removals = [new InventoryItemRemoval(_creature.Id, gear.Id, 1)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext.Creatures.FindAsync(
            [_creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(baseMaximumHp, updated!.MaximumHp);
        Assert.Equal(baseMaximumHp, updated.CurrentHp);
    }

    [Fact]
    public async Task Handle_RemovesFromMultipleCreaturesInOneCall()
    {
        // Arrange
        await GiveToCreature(_item, _creature.Id, 1);
        await GiveToCreature(_stackableItem, _otherCreature.Id, 5);

        // Act
        await _removeHandler.Handle(
            new RemoveInventoryItemsCommand
            {
                Removals =
                [
                    new InventoryItemRemoval(_creature.Id, _item.Id, 1),
                    new InventoryItemRemoval(_otherCreature.Id, _stackableItem.Id, 3),
                ],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var creatureItems = await _getHandler.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(_creature.Id, OwnerType.Creature),
            },
            TestContext.Current.CancellationToken
        );
        var otherCreatureItems = await _getHandler.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(_otherCreature.Id, OwnerType.Creature),
            },
            TestContext.Current.CancellationToken
        );
        Assert.Empty(creatureItems);
        Assert.Equal(2, otherCreatureItems.Single().Quantity);
    }
}
