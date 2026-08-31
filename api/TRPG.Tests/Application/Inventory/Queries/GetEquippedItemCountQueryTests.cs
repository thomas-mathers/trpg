using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

[Collection("Database")]
public sealed class GetEquippedItemCountQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetEquippedItemCountQueryHandler _handler = null!;
    private readonly Guid _ownerId = Guid.NewGuid();

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetEquippedItemCountQueryHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CountsOnlyRequestedAndEquippedItems()
    {
        // Arrange
        var equipped = Builders.MakeItem(WorldId);
        equipped.Ownership.OwnerId = _ownerId;
        equipped.Ownership.OwnerType = OwnerType.Creature;
        equipped.Ownership.EquippedSlot = EquipmentSlot.LeftHand;

        var unequipped = Builders.MakeItem(WorldId);
        unequipped.Ownership.OwnerId = _ownerId;
        unequipped.Ownership.OwnerType = OwnerType.Creature;

        var equippedButNotRequested = Builders.MakeItem(WorldId);
        equippedButNotRequested.Ownership.OwnerId = _ownerId;
        equippedButNotRequested.Ownership.OwnerType = OwnerType.Creature;
        equippedButNotRequested.Ownership.EquippedSlot = EquipmentSlot.RightHand;

        _context.Items.AddRange(equipped, unequipped, equippedButNotRequested);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetEquippedItemCountQuery
            {
                WorldId = WorldId,
                ItemIds = [equipped.Id, unequipped.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(1, result);
    }
}
