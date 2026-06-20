using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public class InventoryServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private PersonService _personService = null!;
    private InventoryService _inventoryService = null!;

    public async Task InitializeAsync()
    {
        _context = db.CreateContext();
        _personService = new PersonService(_context);
        _inventoryService = new InventoryService(_context);
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    private async Task<(Person person, Item item)> SeedPersonAndItem(bool stackable = false)
    {
        var person = Builders.MakePerson();
        await _personService.Add(person);

        var item = Builders.MakeItem(stackable);
        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        return (person, item);
    }

    [Fact]
    public async Task Add_AddsItemToInventory()
    {
        // Arrange
        var (person, item) = await SeedPersonAndItem();

        // Act
        await _inventoryService.Add(person.Id, item.Id, 1);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(person.Id);
        Assert.Single(items);
        Assert.Equal(item.Id, items[0].ItemId);
        Assert.Equal(1, items[0].Quantity);
    }

    [Fact]
    public async Task Add_StackableItem_IncrementsQuantity()
    {
        // Arrange
        var (person, item) = await SeedPersonAndItem(stackable: true);
        await _inventoryService.Add(person.Id, item.Id, 3);

        // Act
        await _inventoryService.Add(person.Id, item.Id, 2);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(person.Id);
        Assert.Single(items);
        Assert.Equal(5, items[0].Quantity);
    }

    [Fact]
    public async Task Add_NonStackableItem_CreatesNewEntry()
    {
        // Arrange
        var (person, item) = await SeedPersonAndItem(stackable: false);
        await _inventoryService.Add(person.Id, item.Id, 1);

        // Act
        await _inventoryService.Add(person.Id, item.Id, 1);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(person.Id);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Remove_DecreasesQuantity()
    {
        // Arrange
        var (person, item) = await SeedPersonAndItem(stackable: true);
        await _inventoryService.Add(person.Id, item.Id, 5);

        // Act
        await _inventoryService.Remove(person.Id, item.Id, 3);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(person.Id);
        Assert.Single(items);
        Assert.Equal(2, items[0].Quantity);
    }

    [Fact]
    public async Task Remove_RemovesEntry_WhenQuantityReachesZero()
    {
        // Arrange
        var (person, item) = await SeedPersonAndItem();
        await _inventoryService.Add(person.Id, item.Id, 1);

        // Act
        await _inventoryService.Remove(person.Id, item.Id, 1);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(person.Id);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Remove_Throws_WhenItemNotInInventory()
    {
        // Arrange
        var (person, item) = await SeedPersonAndItem();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _inventoryService.Remove(person.Id, item.Id, 1));
    }

    [Fact]
    public async Task Equip_SetsEquippedSlot()
    {
        // Arrange
        var (person, item) = await SeedPersonAndItem();
        await _inventoryService.Add(person.Id, item.Id, 1);

        // Act
        await _inventoryService.Equip(person.Id, item.Id, EquipmentSlot.RightHand);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(person.Id);
        Assert.Equal(EquipmentSlot.RightHand, items[0].EquippedSlot);
    }

    [Fact]
    public async Task Equip_UnequipsPrevious_WhenSlotAlreadyOccupied()
    {
        // Arrange
        var person = Builders.MakePerson();
        await _personService.Add(person);

        var item1 = Builders.MakeItem();
        var item2 = Builders.MakeItem();
        
        _context.Items.AddRange(item1, item2);
        
        await _context.SaveChangesAsync();

        await _inventoryService.Add(person.Id, item1.Id, 1);
        await _inventoryService.Add(person.Id, item2.Id, 1);
        await _inventoryService.Equip(person.Id, item1.Id, EquipmentSlot.RightHand);

        // Act
        await _inventoryService.Equip(person.Id, item2.Id, EquipmentSlot.RightHand);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(person.Id);
        var equipped = items.Where(i => i.EquippedSlot == EquipmentSlot.RightHand).ToList();
        
        Assert.Single(equipped);
        Assert.Equal(item2.Id, equipped[0].ItemId);
    }

    [Fact]
    public async Task Unequip_ClearsSlot()
    {
        // Arrange
        var (person, item) = await SeedPersonAndItem();
        await _inventoryService.Add(person.Id, item.Id, 1);
        await _inventoryService.Equip(person.Id, item.Id, EquipmentSlot.LeftHand);

        // Act
        await _inventoryService.Unequip(person.Id, EquipmentSlot.LeftHand);

        // Assert
        var items = await _inventoryService.GetAllByPersonId(person.Id);
        Assert.Null(items[0].EquippedSlot);
    }

    [Fact]
    public async Task Unequip_Throws_WhenSlotEmpty()
    {
        // Arrange
        var (person, _) = await SeedPersonAndItem();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _inventoryService.Unequip(person.Id, EquipmentSlot.Helm));
    }
}
