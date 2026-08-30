using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

[Collection("Database")]
public sealed class GetKeyItemIdsByOwnerQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetKeyItemIdsByOwnerQueryHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();
    private readonly Key _key = Builders.MakeKey();
    private readonly Weapon _weapon = Builders.MakeWeapon();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetKeyItemIdsByOwnerQueryHandler(_context);

        _context.Creatures.Add(_creature);
        _context.Items.AddRange(_key, _weapon);

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsEmptySet_WhenOwnerHasNoKeys()
    {
        // Act
        var keyItemIds = await _handler.Handle(
            new GetKeyItemIdsByOwnerQuery
            {
                Owner = new ItemOwnerReference(_creature.Id, OwnerType.Creature),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(keyItemIds);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyKeyItems_ExcludingOtherItemTypesTheOwnerHolds()
    {
        // Arrange
        _key.Quantity = 1;
        _key.Ownership.OwnerId = _creature.Id;
        _key.Ownership.OwnerType = OwnerType.Creature;
        _weapon.Quantity = 1;
        _weapon.Ownership.OwnerId = _creature.Id;
        _weapon.Ownership.OwnerType = OwnerType.Creature;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var keyItemIds = await _handler.Handle(
            new GetKeyItemIdsByOwnerQuery
            {
                Owner = new ItemOwnerReference(_creature.Id, OwnerType.Creature),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([_key.Id], keyItemIds);
    }

    [Fact]
    public async Task Handle_ExcludesKeys_WithZeroQuantity()
    {
        // Arrange
        _key.Quantity = 0;
        _key.Ownership.OwnerId = _creature.Id;
        _key.Ownership.OwnerType = OwnerType.Creature;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var keyItemIds = await _handler.Handle(
            new GetKeyItemIdsByOwnerQuery
            {
                Owner = new ItemOwnerReference(_creature.Id, OwnerType.Creature),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(keyItemIds);
    }
}
