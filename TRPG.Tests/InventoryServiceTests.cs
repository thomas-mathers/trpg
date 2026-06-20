using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public class InventoryServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private InventoryService _inventoryService = null!;
    private Person _person = null!;
    private Item _item = null!;
    private Item _stackableItem = null!;

    public async Task InitializeAsync()
    {
        _context = db.CreateContext();
        _inventoryService = new InventoryService(_context);
        
        _person = Builders.MakePerson();
        _item = Builders.MakeItem(stackable: false);
        _stackableItem = Builders.MakeItem(stackable: true);
        
        _context.Persons.Add(_person);
        _context.Items.Add(_item);
        _context.Items.Add(_stackableItem);
        
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task Add_AddsItemToInventory()
    {
        // Arrange & Act
        await _inventoryService.Add(_person.Id, _item.Id, 1);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(_person.Id);
        Assert.Single(items);
        Assert.Equal(_item.Id, items[0].ItemId);
        Assert.Equal(1, items[0].Quantity);
    }

    [Fact]
    public async Task Add_StackableItem_IncrementsQuantity()
    {
        // Arrange
        await _inventoryService.Add(_person.Id, _stackableItem.Id, 3);

        // Act
        await _inventoryService.Add(_person.Id, _stackableItem.Id, 2);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(_person.Id);
        Assert.Single(items);
        Assert.Equal(5, items[0].Quantity);
    }

    [Fact]
    public async Task Add_NonStackableItem_CreatesNewEntry()
    {
        // Arrange
        await _inventoryService.Add(_person.Id, _item.Id, 1);

        // Act
        await _inventoryService.Add(_person.Id, _item.Id, 1);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(_person.Id);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Remove_DecreasesQuantity()
    {
        // Arrange
        await _inventoryService.Add(_person.Id, _stackableItem.Id, 5);

        // Act
        await _inventoryService.Remove(_person.Id, _stackableItem.Id, 3);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(_person.Id);
        Assert.Single(items);
        Assert.Equal(2, items[0].Quantity);
    }

    [Fact]
    public async Task Remove_RemovesEntry_WhenQuantityReachesZero()
    {
        // Arrange
        await _inventoryService.Add(_person.Id, _item.Id, 1);

        // Act
        await _inventoryService.Remove(_person.Id, _item.Id, 1);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(_person.Id);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Remove_Throws_WhenItemNotInInventory()
    {
        // Arrange & Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _inventoryService.Remove(_person.Id, _item.Id, 1));
    }

    [Fact]
    public async Task Equip_SetsEquippedSlot()
    {
        // Arrange
        await _inventoryService.Add(_person.Id, _item.Id, 1);

        // Act
        await _inventoryService.Equip(_person.Id, _item.Id, EquipmentSlot.RightHand);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(_person.Id);
        Assert.Equal(EquipmentSlot.RightHand, items[0].EquippedSlot);
    }

    [Fact]
    public async Task Equip_UnequipsPrevious_WhenSlotAlreadyOccupied()
    {
        // Arrange
        await _inventoryService.Add(_person.Id, _item.Id, 1);
        await _inventoryService.Add(_person.Id, _stackableItem.Id, 1);
        await _inventoryService.Equip(_person.Id, _item.Id, EquipmentSlot.RightHand);

        // Act
        await _inventoryService.Equip(_person.Id, _stackableItem.Id, EquipmentSlot.RightHand);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(_person.Id);
        var equipped = items.Where(i => i.EquippedSlot == EquipmentSlot.RightHand).ToList();
        
        Assert.Single(equipped);
        Assert.Equal(_stackableItem.Id, equipped[0].ItemId);
    }

    [Fact]
    public async Task Unequip_ClearsSlot()
    {
        // Arrange
        await _inventoryService.Add(_person.Id, _item.Id, 1);
        await _inventoryService.Equip(_person.Id, _item.Id, EquipmentSlot.LeftHand);

        // Act
        await _inventoryService.Unequip(_person.Id, EquipmentSlot.LeftHand);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(_person.Id);
        Assert.Null(items[0].EquippedSlot);
    }

    [Fact]
    public async Task Unequip_Throws_WhenSlotEmpty()
    {
        // Arrange & Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _inventoryService.Unequip(_person.Id, EquipmentSlot.Helm));
    }
}
