using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

[Collection("Database")]
public sealed class GetItemNamesByIdsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetItemNamesByIdsQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetItemNamesByIdsQueryHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsNamesForSelectedItems()
    {
        // Arrange
        var selectedSword = MakeItem(WorldId, "Selected Sword");
        var selectedShield = MakeItem(WorldId, "Selected Shield");
        var unselectedItem = MakeItem(WorldId, "Unselected Item");
        _context.Items.AddRange(selectedSword, selectedShield, unselectedItem);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var names = await _handler.Handle(
            new GetItemNamesByIdsQuery
            {
                WorldId = WorldId,
                ItemIds = [selectedSword.Id, selectedShield.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, names.Count);
        Assert.Equal(selectedSword.Name, names[selectedSword.Id]);
        Assert.Equal(selectedShield.Name, names[selectedShield.Id]);
        Assert.DoesNotContain(unselectedItem.Id, names.Keys);
    }

    [Fact]
    public async Task Handle_ExcludesItemsFromAnotherWorld()
    {
        // Arrange
        var selectedItem = MakeItem(WorldId, "Local Item");
        var foreignItem = MakeItem(Guid.NewGuid(), "Foreign Item");
        _context.Items.AddRange(selectedItem, foreignItem);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var names = await _handler.Handle(
            new GetItemNamesByIdsQuery
            {
                WorldId = WorldId,
                ItemIds = [selectedItem.Id, foreignItem.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([selectedItem.Id], names.Keys);
        Assert.Equal(selectedItem.Name, names[selectedItem.Id]);
    }

    private static Item MakeItem(Guid worldId, string name) =>
        new()
        {
            WorldId = worldId,
            Name = name,
            Description = "A test item",
            Weight = 1,
        };
}
