using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class RemoveInventoryItemCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetInventoryByCreatureIdQueryHandler _getHandler = null!;
    private RemoveInventoryItemCommandHandler _removeHandler = null!;
    private readonly Creature _creature = Builders.MakeCreature();
    private readonly Item _item = Builders.MakeItem();
    private readonly Item _stackableItem = Builders.MakeConsumableItem();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _removeHandler = new RemoveInventoryItemCommandHandler(_context);
        _getHandler = new GetInventoryByCreatureIdQueryHandler(_context);

        _context.Creatures.Add(_creature);
        _context.Items.AddRange(_item, _stackableItem);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task GiveToCreature(Item item, int quantity)
    {
        item.Quantity = quantity;
        item.Ownership.OwnerId = _creature.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_DecreasesQuantity()
    {
        // Arrange
        await GiveToCreature(_stackableItem, 5);

        // Act
        await _removeHandler.Handle(
            new RemoveInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _stackableItem.Id,
                Quantity = 3,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Single(items);
        Assert.Equal(2, items[0].Quantity);
    }

    [Fact]
    public async Task Handle_RemovesEntry_WhenQuantityReachesZero()
    {
        // Arrange
        await GiveToCreature(_item, 1);

        // Act
        await _removeHandler.Handle(
            new RemoveInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = _item.Id,
                Quantity = 1,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _getHandler.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = _creature.Id },
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
                new RemoveInventoryItemCommand
                {
                    CreatureId = _creature.Id,
                    ItemId = _item.Id,
                    Quantity = 1,
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
        var gear = Builders.MakeArmorItem(
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
        await GiveToCreature(gear, 1);
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
            new RemoveInventoryItemCommand
            {
                CreatureId = _creature.Id,
                ItemId = gear.Id,
                Quantity = 1,
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
}
