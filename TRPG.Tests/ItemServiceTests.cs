using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class ItemServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private TrpgDbContext _context = null!;
    private Item _item = null!;
    private ItemService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new ItemService(_context);

        _item = Builders.MakeItem();
        _context.Items.Add(_item);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Add_PersistsItem() {
        // Arrange
        var item = Builders.MakeItem();

        // Act
        await _service.Add(item, TestContext.Current.CancellationToken);

        // Assert
        var found = await _context.Items.FindAsync([item.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(found);
        Assert.Equal(item.Name, found.Name);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound() {
        // Act
        var result = await _service.GetById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_ReturnsItem_WhenExists() {
        // Act
        var result = await _service.GetById(_item.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_item.Id, result.Id);
    }

    [Fact]
    public async Task Update_SavesChanges() {
        // Arrange — build updated entity in a fresh context to avoid tracking conflict with _item
        var updated = new Item {
            Id = _item.Id,
            WorldId = _item.WorldId,
            Name = _item.Name,
            Description = "Updated description",
            Weight = _item.Weight,
            GoldValue = 999
        };

        // Act
        await using var updateContext = db.CreateContext();
        await new ItemService(updateContext).Update(updated, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var found = await verifyContext.Items.FindAsync([_item.Id], TestContext.Current.CancellationToken);
        Assert.Equal("Updated description", found!.Description);
        Assert.Equal(999, found.GoldValue);
    }

    [Fact]
    public async Task Delete_RemovesItem() {
        // Arrange
        var item = Builders.MakeItem();
        await _service.Add(item, TestContext.Current.CancellationToken);

        // Act
        await _service.Delete(item.Id, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var found = await verifyContext.Items.FindAsync([item.Id], TestContext.Current.CancellationToken);
        Assert.Null(found);
    }
}