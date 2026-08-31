using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Inventory;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory;

[Collection("Database")]
public sealed class TradeOfferValidatorTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private TradeOfferValidator _validator = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Workstation _workstation = Builders.MakeWorkstation(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _validator = _serviceProvider.GetRequiredService<TradeOfferValidator>();

        _context.Creatures.Add(_player);
        _context.Props.Add(_workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Item> SeedItem(Item item, ItemOwnerReference owner)
    {
        item.Ownership.OwnerId = owner.Id;
        item.Ownership.OwnerType = owner.Type;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    [Fact]
    public async Task Validate_ReturnsOfferValues_WhenOffersAreAvailable()
    {
        // Arrange
        var playerItem = await SeedItem(
            Builders.MakeWeapon(WorldId, quantity: 1),
            new(_player.Id, OwnerType.Creature)
        );
        var shopItem = await SeedItem(
            Builders.MakeArmor(WorldId, quantity: 1),
            new(_workstation.Id, OwnerType.Workstation)
        );

        // Act
        var result = await _validator.Validate(
            _player.Id,
            _workstation.Id,
            [new ItemSelection(playerItem.Id, 1)],
            [new ItemSelection(shopItem.Id, 1)],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            new ItemOwnerReference(_workstation.Id, OwnerType.Workstation),
            result.ShopOwner
        );
        Assert.Equal(50, result.PlayerOfferValue);
        Assert.Equal(40, result.ShopOfferValue);
    }

    [Fact]
    public async Task Validate_ReturnsOfferValues_WhenPlayerOfferIsWorthLessThanShopOffer()
    {
        // Arrange
        var playerItem = await SeedItem(
            Builders.MakeArmor(WorldId, quantity: 1),
            new(_player.Id, OwnerType.Creature)
        );
        var shopItem = await SeedItem(
            Builders.MakeWeapon(WorldId, quantity: 1),
            new(_workstation.Id, OwnerType.Workstation)
        );

        // Act
        var result = await _validator.Validate(
            _player.Id,
            _workstation.Id,
            [new ItemSelection(playerItem.Id, 1)],
            [new ItemSelection(shopItem.Id, 1)],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(40, result.PlayerOfferValue);
        Assert.Equal(50, result.ShopOfferValue);
    }

    [Fact]
    public async Task Validate_ThrowsEntityNotFoundException_WhenPlayerDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _validator.Validate(
                Guid.NewGuid(),
                _workstation.Id,
                [],
                [],
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Validate_ThrowsEntityNotFoundException_WhenWorkstationDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _validator.Validate(
                _player.Id,
                Guid.NewGuid(),
                [],
                [],
                TestContext.Current.CancellationToken
            )
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Validate_Throws_WhenOfferedQuantityIsNotAvailable(int quantity)
    {
        // Arrange
        var playerItem = await SeedItem(
            Builders.MakeWeapon(WorldId, quantity: 1),
            new(_player.Id, OwnerType.Creature)
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _validator.Validate(
                _player.Id,
                _workstation.Id,
                [new ItemSelection(playerItem.Id, quantity)],
                [],
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Validate_Throws_WhenOfferedItemIsNotOwnedByOfferer()
    {
        // Arrange
        var shopItem = await SeedItem(
            Builders.MakeWeapon(WorldId, quantity: 1),
            new(_workstation.Id, OwnerType.Workstation)
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _validator.Validate(
                _player.Id,
                _workstation.Id,
                [new ItemSelection(shopItem.Id, 1)],
                [],
                TestContext.Current.CancellationToken
            )
        );
    }
}
