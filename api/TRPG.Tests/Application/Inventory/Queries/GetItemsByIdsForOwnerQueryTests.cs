using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

[Collection("Database")]
public sealed class GetItemsByIdsForOwnerQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetItemsByIdsForOwnerQueryHandler _handler = null!;
    private readonly Creature _owner = Builders.MakeCreature(WorldId);
    private readonly Creature _otherOwner = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetItemsByIdsForOwnerQueryHandler>();

        _context.Creatures.AddRange(_owner, _otherOwner);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyRequestedItemsOwnedByOwner_ExcludingOtherOwnersAndOwnerTypes()
    {
        // Arrange
        var requestedItem = Builders.MakeItem(WorldId);
        requestedItem.Ownership.OwnerId = _owner.Id;
        requestedItem.Ownership.OwnerType = OwnerType.Creature;

        var unrequestedItem = Builders.MakeItem(WorldId);
        unrequestedItem.Ownership.OwnerId = _owner.Id;
        unrequestedItem.Ownership.OwnerType = OwnerType.Creature;

        var sameIdDifferentOwner = Builders.MakeItem(WorldId);
        sameIdDifferentOwner.Ownership.OwnerId = _otherOwner.Id;
        sameIdDifferentOwner.Ownership.OwnerType = OwnerType.Creature;

        var sameOwnerDifferentType = Builders.MakeItem(WorldId);
        sameOwnerDifferentType.Ownership.OwnerId = _owner.Id;
        sameOwnerDifferentType.Ownership.OwnerType = OwnerType.Workstation;

        _context.Items.AddRange(
            requestedItem,
            unrequestedItem,
            sameIdDifferentOwner,
            sameOwnerDifferentType
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetItemsByIdsForOwnerQuery
            {
                OwnerId = _owner.Id,
                OwnerType = OwnerType.Creature,
                ItemIds = [requestedItem.Id, sameIdDifferentOwner.Id, sameOwnerDifferentType.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var resultItem = Assert.Single(result);
        Assert.Equal(requestedItem.Id, resultItem.Id);
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoItemsMatch()
    {
        // Act
        var result = await _handler.Handle(
            new GetItemsByIdsForOwnerQuery
            {
                OwnerId = _owner.Id,
                OwnerType = OwnerType.Creature,
                ItemIds = [Guid.NewGuid()],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }
}
