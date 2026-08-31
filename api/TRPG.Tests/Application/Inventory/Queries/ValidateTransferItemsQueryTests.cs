using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

[Collection("Database")]
public sealed class ValidateTransferItemsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ValidateTransferItemsQueryHandler _handler = null!;
    private readonly Creature _owner = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ValidateTransferItemsQueryHandler>();

        _context.Creatures.Add(_owner);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTransferItems_WhenSelectionIsValid()
    {
        // Arrange
        var item = Builders.MakeItem(WorldId);
        item.Quantity = 5;
        item.Ownership.OwnerId = _owner.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ValidateTransferItemsQuery
            {
                From = new ItemOwnerReference(_owner.Id, OwnerType.Creature),
                Selections = [new ItemSelection(item.Id, 5)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var transferItem = Assert.Single(result);
        Assert.Equal(item.Id, transferItem.Item.Id);
        Assert.Equal(5, transferItem.Quantity);
    }

    [Fact]
    public async Task Handle_Throws_WhenRequestedQuantityExceedsAvailable()
    {
        // Arrange
        var item = Builders.MakeItem(WorldId);
        item.Quantity = 2;
        item.Ownership.OwnerId = _owner.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new ValidateTransferItemsQuery
                {
                    From = new ItemOwnerReference(_owner.Id, OwnerType.Creature),
                    Selections = [new ItemSelection(item.Id, 3)],
                },
                TestContext.Current.CancellationToken
            )
        );
    }
}
