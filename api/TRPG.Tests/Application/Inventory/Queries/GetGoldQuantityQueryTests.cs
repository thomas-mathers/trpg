using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

[Collection("Database")]
public sealed class GetGoldQuantityQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetGoldQuantityQueryHandler _handler = null!;
    private readonly Creature _owner = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetGoldQuantityQueryHandler>();

        _context.Creatures.Add(_owner);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTheOwnersGoldQuantity()
    {
        // Arrange
        var gold = Builders.MakeGold(WorldId, quantity: 42);
        gold.Ownership.OwnerId = _owner.Id;
        gold.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(gold);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var quantity = await _handler.Handle(
            new GetGoldQuantityQuery
            {
                Owner = new ItemOwnerReference(_owner.Id, OwnerType.Creature),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(42, quantity);
    }

    [Fact]
    public async Task Handle_ReturnsZero_WhenTheOwnerHasNoGold()
    {
        // Act
        var quantity = await _handler.Handle(
            new GetGoldQuantityQuery
            {
                Owner = new ItemOwnerReference(_owner.Id, OwnerType.Creature),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(0, quantity);
    }
}
