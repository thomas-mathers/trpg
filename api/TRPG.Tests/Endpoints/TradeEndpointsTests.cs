using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Contracts.Trading.Requests;
using TRPG.Contracts.Trading.Responses;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Endpoints;

[Collection("Endpoints")]
public sealed class TradeEndpointsTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private TestApiClient _client = null!;
    private readonly World _world = Builders.MakeWorld();
    private Creature _player = null!;
    private Workstation _workstation = null!;

    public async ValueTask InitializeAsync()
    {
        _client = fixture.CreateApiClient();
        _player = Builders.MakeCreature(_world.Id);
        _workstation = Builders.MakeWorkstation(_world.Id);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        context.Worlds.Add(_world);
        context.Creatures.Add(_player);
        context.Props.Add(_workstation);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<Item> SeedItem(Item item, Guid ownerId, OwnerType ownerType)
    {
        item.Ownership.OwnerId = ownerId;
        item.Ownership.OwnerType = ownerType;
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        context.Items.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    [Fact]
    public async Task GetTrade_ReturnsPlayerAndShopInventories()
    {
        // Arrange
        var playerItem = await SeedItem(
            Builders.MakeWeaponItem(_world.Id, quantity: 1),
            _player.Id,
            OwnerType.Creature
        );
        var shopItem = await SeedItem(
            Builders.MakeArmorItem(_world.Id, quantity: 1),
            _workstation.Id,
            OwnerType.Workstation
        );

        // Act
        var response = await _client.GetAsync(
            "GetTrade",
            new { playerId = _player.Id, workstationId = _workstation.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var trade = await _client.ReadContentFromJsonAsync<TradeSnapshot>(
            response,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(playerItem.Id, Assert.Single(trade!.PlayerInventory.Items).ItemId);
        Assert.Equal(shopItem.Id, Assert.Single(trade.ShopInventory.Items).ItemId);
    }

    [Fact]
    public async Task ProposeTrade_ReturnsAccepted_WhenOfferMeetsShopValue()
    {
        // Arrange
        var playerItem = await SeedItem(
            Builders.MakeWeaponItem(_world.Id, quantity: 1),
            _player.Id,
            OwnerType.Creature
        );
        var shopItem = await SeedItem(
            Builders.MakeArmorItem(_world.Id, quantity: 1),
            _workstation.Id,
            OwnerType.Workstation
        );

        // Act
        var response = await _client.PostAsJsonAsync(
            "ProposeTrade",
            new TradeRequest(
                [new ItemSelection(playerItem.Id, 1)],
                [new ItemSelection(shopItem.Id, 1)]
            ),
            new { playerId = _player.Id, workstationId = _workstation.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var proposal = await _client.ReadContentFromJsonAsync<TradeProposalResponse>(
            response,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(TradeProposalStatus.Accepted, proposal!.Status);
    }

    [Fact]
    public async Task ProposeTrade_ReturnsNotFound_WhenWorkstationDoesNotExist()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "ProposeTrade",
            new TradeRequest([], []),
            new { playerId = _player.Id, workstationId = Guid.NewGuid() },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CompleteTrade_TransfersBothOffers_WhenOfferIsAccepted()
    {
        // Arrange
        var playerItem = await SeedItem(
            Builders.MakeWeaponItem(_world.Id, quantity: 1),
            _player.Id,
            OwnerType.Creature
        );
        var shopItem = await SeedItem(
            Builders.MakeArmorItem(_world.Id, quantity: 1),
            _workstation.Id,
            OwnerType.Workstation
        );

        // Act
        var response = await _client.PostAsJsonAsync(
            "CompleteTrade",
            new TradeRequest(
                [new ItemSelection(playerItem.Id, 1)],
                [new ItemSelection(shopItem.Id, 1)]
            ),
            new { playerId = _player.Id, workstationId = _workstation.Id },
            new Dictionary<string, object?> { ["worldId"] = _world.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var transferredPlayerItem = await verifyContext.Items.SingleAsync(
            item => item.Id == playerItem.Id,
            TestContext.Current.CancellationToken
        );
        var transferredShopItem = await verifyContext.Items.SingleAsync(
            item => item.Id == shopItem.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_workstation.Id, transferredPlayerItem.Ownership.OwnerId);
        Assert.Equal(_player.Id, transferredShopItem.Ownership.OwnerId);
    }
}
