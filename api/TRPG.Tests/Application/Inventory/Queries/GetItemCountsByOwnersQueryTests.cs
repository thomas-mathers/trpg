using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

[Collection("Database")]
public sealed class GetItemCountsByOwnersQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetItemCountsByOwnersQueryHandler _handler = null!;
    private readonly Creature _first = Builders.MakeCreature(WorldId);
    private readonly Creature _second = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetItemCountsByOwnersQueryHandler>();

        _context.Creatures.AddRange(_first, _second);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CountsOwnedItemsPerOwner_ExcludingZeroQuantityAndOtherOwnerTypes()
    {
        // Arrange
        var firstItemA = Builders.MakeItem(WorldId);
        firstItemA.Quantity = 1;
        firstItemA.Ownership.OwnerId = _first.Id;
        firstItemA.Ownership.OwnerType = OwnerType.Creature;

        var firstItemB = Builders.MakeItem(WorldId);
        firstItemB.Quantity = 1;
        firstItemB.Ownership.OwnerId = _first.Id;
        firstItemB.Ownership.OwnerType = OwnerType.Creature;

        var secondItem = Builders.MakeItem(WorldId);
        secondItem.Quantity = 1;
        secondItem.Ownership.OwnerId = _second.Id;
        secondItem.Ownership.OwnerType = OwnerType.Creature;

        var zeroQuantityItem = Builders.MakeItem(WorldId);
        zeroQuantityItem.Quantity = 0;
        zeroQuantityItem.Ownership.OwnerId = _first.Id;
        zeroQuantityItem.Ownership.OwnerType = OwnerType.Creature;

        var workstationOwnedItem = Builders.MakeItem(WorldId);
        workstationOwnedItem.Quantity = 1;
        workstationOwnedItem.Ownership.OwnerId = _first.Id;
        workstationOwnedItem.Ownership.OwnerType = OwnerType.Workstation;

        _context.Items.AddRange(
            firstItemA,
            firstItemB,
            secondItem,
            zeroQuantityItem,
            workstationOwnedItem
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var counts = await _handler.Handle(
            new GetItemCountsByOwnersQuery
            {
                OwnerIds = [_first.Id, _second.Id],
                OwnerType = OwnerType.Creature,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, counts[_first.Id]);
        Assert.Equal(1, counts[_second.Id]);
    }

    [Fact]
    public async Task Handle_OmitsOwnersWithNoItems()
    {
        // Act
        var counts = await _handler.Handle(
            new GetItemCountsByOwnersQuery
            {
                OwnerIds = [_first.Id],
                OwnerType = OwnerType.Creature,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(counts);
    }
}
