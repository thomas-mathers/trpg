using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class InventoryServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private InventoryService _inventoryService = null!;
    private Item _item = null!;
    private Item _stackableItem = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _inventoryService = new InventoryService(_context);

        _creature = Builders.MakeCreature();
        _item = Builders.MakeItem();
        _stackableItem = Builders.MakeConsumableItem();

        _context.Creatures.Add(_creature);
        _context.Items.Add(_item);
        _context.Items.Add(_stackableItem);

        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Add_AddsItemToInventory()
    {
        // Arrange & Act
        await _inventoryService.Add(
            _creature.Id,
            _item.Id,
            1,
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _inventoryService.GetAllByCreatureId(
            _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Single(items);
        Assert.Equal(_item.Id, items.First().ItemId);
        Assert.Equal(1, items.First().Quantity);
    }

    [Fact]
    public async Task Add_StackableItem_IncrementsQuantity()
    {
        // Arrange
        await _inventoryService.Add(
            _creature.Id,
            _stackableItem.Id,
            3,
            TestContext.Current.CancellationToken
        );

        // Act
        await _inventoryService.Add(
            _creature.Id,
            _stackableItem.Id,
            2,
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _inventoryService.GetAllByCreatureId(
            _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Single(items);
        Assert.Equal(5, items.First().Quantity);
    }

    [Fact]
    public async Task Add_NonStackableItem_CreatesNewEntry()
    {
        // Arrange
        await _inventoryService.Add(
            _creature.Id,
            _item.Id,
            1,
            TestContext.Current.CancellationToken
        );

        // Act
        await _inventoryService.Add(
            _creature.Id,
            _item.Id,
            1,
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _inventoryService.GetAllByCreatureId(
            _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Remove_DecreasesQuantity()
    {
        // Arrange
        await _inventoryService.Add(
            _creature.Id,
            _stackableItem.Id,
            5,
            TestContext.Current.CancellationToken
        );

        // Act
        await _inventoryService.Remove(
            _creature.Id,
            _stackableItem.Id,
            3,
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _inventoryService.GetAllByCreatureId(
            _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Single(items);
        Assert.Equal(2, items.First().Quantity);
    }

    [Fact]
    public async Task Remove_RemovesEntry_WhenQuantityReachesZero()
    {
        // Arrange
        await _inventoryService.Add(
            _creature.Id,
            _item.Id,
            1,
            TestContext.Current.CancellationToken
        );

        // Act
        await _inventoryService.Remove(
            _creature.Id,
            _item.Id,
            1,
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _inventoryService.GetAllByCreatureId(
            _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Empty(items);
    }

    [Fact]
    public async Task Remove_Throws_WhenItemNotInInventory()
    {
        // Arrange & Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _inventoryService.Remove(
                _creature.Id,
                _item.Id,
                1,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Equip_SetsEquippedSlot()
    {
        // Arrange
        await _inventoryService.Add(
            _creature.Id,
            _item.Id,
            1,
            TestContext.Current.CancellationToken
        );

        // Act
        await _inventoryService.Equip(
            _creature.Id,
            _item.Id,
            EquipmentSlot.RightHand,
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _inventoryService.GetAllByCreatureId(
            _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(EquipmentSlot.RightHand, items.First().EquippedSlot);
    }

    [Fact]
    public async Task Equip_UnequipsPrevious_WhenSlotAlreadyOccupied()
    {
        // Arrange
        await _inventoryService.Add(
            _creature.Id,
            _item.Id,
            1,
            TestContext.Current.CancellationToken
        );
        await _inventoryService.Add(
            _creature.Id,
            _stackableItem.Id,
            1,
            TestContext.Current.CancellationToken
        );
        await _inventoryService.Equip(
            _creature.Id,
            _item.Id,
            EquipmentSlot.RightHand,
            TestContext.Current.CancellationToken
        );

        // Act
        await _inventoryService.Equip(
            _creature.Id,
            _stackableItem.Id,
            EquipmentSlot.RightHand,
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _inventoryService.GetAllByCreatureId(
            _creature.Id,
            TestContext.Current.CancellationToken
        );
        var equipped = items.Where(i => i.EquippedSlot == EquipmentSlot.RightHand).ToList();

        Assert.Single(equipped);
        Assert.Equal(_stackableItem.Id, equipped[0].ItemId);
    }

    [Fact]
    public async Task Unequip_ClearsSlot()
    {
        // Arrange
        await _inventoryService.Add(
            _creature.Id,
            _item.Id,
            1,
            TestContext.Current.CancellationToken
        );
        await _inventoryService.Equip(
            _creature.Id,
            _item.Id,
            EquipmentSlot.LeftHand,
            TestContext.Current.CancellationToken
        );

        // Act
        await _inventoryService.Unequip(
            _creature.Id,
            EquipmentSlot.LeftHand,
            TestContext.Current.CancellationToken
        );

        // Assert
        var items = await _inventoryService.GetAllByCreatureId(
            _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Null(items.First().EquippedSlot);
    }

    [Fact]
    public async Task Unequip_Throws_WhenSlotEmpty()
    {
        // Arrange & Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _inventoryService.Unequip(
                _creature.Id,
                EquipmentSlot.Helm,
                TestContext.Current.CancellationToken
            )
        );
    }
}
