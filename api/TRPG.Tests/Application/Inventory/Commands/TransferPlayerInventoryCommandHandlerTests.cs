using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class TransferPlayerInventoryCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private TransferPlayerInventoryCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);
    private readonly Container _container = Builders.MakeContainer(WorldId, LocationId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<TransferPlayerInventoryCommandHandler>();

        _context.Creatures.Add(_player);
        _context.Props.Add(_container);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_MovesSelectedItems_FromPlayerToContainer()
    {
        // Arrange
        var item = Builders.MakeWeaponItem(WorldId);
        item.Quantity = 1;
        item.Ownership.OwnerId = _player.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new TransferPlayerInventoryCommand
            {
                To = new ItemOwnerReference(_container.Id, OwnerType.Container),
                Items = [new ItemSelection(item.Id, 1)],
                PlayerId = _player.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var movedItem = await verifyContext.Items.SingleAsync(
            movedItem => movedItem.Id == item.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_container.Id, movedItem.Ownership.OwnerId);
        Assert.Equal(OwnerType.Container, movedItem.Ownership.OwnerType);
    }
}
