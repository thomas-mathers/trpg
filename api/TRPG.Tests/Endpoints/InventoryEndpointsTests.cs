using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Serialization;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Inventory.Requests;
using TRPG.Inventory.Responses;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Endpoints;

[Collection("Endpoints")]
public sealed class InventoryEndpointsTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private static readonly Guid LocationId = Guid.NewGuid();

    private TestApiClient _client = null!;
    private Guid _worldId;
    private Creature _fromCreature = null!;
    private Creature _toCreature = null!;

    public async ValueTask InitializeAsync()
    {
        _client = fixture.CreateApiClient();

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var world = Builders.MakeWorld();
        _fromCreature = Builders.MakeCreature(world.Id, locationId: LocationId);
        _toCreature = Builders.MakeCreature(world.Id, locationId: LocationId);

        context.Worlds.Add(world);
        context.Creatures.AddRange(_fromCreature, _toCreature);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _worldId = world.Id;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task<Item> SeedItemOnFromCreature()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var item = Builders.MakeWeapon(_worldId);
        item.Quantity = 1;
        item.Ownership.OwnerId = _fromCreature.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        context.Items.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    [Fact]
    public async Task Transfer_MovesItem_WhenCreaturesAreNearby()
    {
        // Arrange
        var item = await SeedItemOnFromCreature();

        // Act
        var response = await _client.PostAsJsonAsync(
            "TransferInventory",
            new InventoryTransferRequest(
                new OwnerReferenceRequest(_fromCreature.Id, OwnerType.Creature),
                new OwnerReferenceRequest(_toCreature.Id, OwnerType.Creature),
                [new ItemSelection(item.Id, 1)]
            ),
            routeValues: new { playerId = _toCreature.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var movedItem = await verifyContext.Items.SingleAsync(
            i => i.Id == item.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(_toCreature.Id, movedItem.Ownership.OwnerId);
    }

    [Fact]
    public async Task Transfer_ReturnsBadRequest_WhenCreaturesAreNotNearby()
    {
        // Arrange
        var item = await SeedItemOnFromCreature();
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var farCreature = Builders.MakeCreature(_worldId, locationId: Guid.NewGuid());
        context.Creatures.Add(farCreature);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await _client.PostAsJsonAsync(
            "TransferInventory",
            new InventoryTransferRequest(
                new OwnerReferenceRequest(_fromCreature.Id, OwnerType.Creature),
                new OwnerReferenceRequest(farCreature.Id, OwnerType.Creature),
                [new ItemSelection(item.Id, 1)]
            ),
            routeValues: new { playerId = _fromCreature.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_ReturnsNotFound_WhenFromCreatureDoesNotExist()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "TransferInventory",
            new InventoryTransferRequest(
                new OwnerReferenceRequest(Guid.NewGuid(), OwnerType.Creature),
                new OwnerReferenceRequest(_toCreature.Id, OwnerType.Creature),
                []
            ),
            routeValues: new { playerId = _toCreature.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_ReturnsNotFound_WhenToCreatureDoesNotExist()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "TransferInventory",
            new InventoryTransferRequest(
                new OwnerReferenceRequest(_fromCreature.Id, OwnerType.Creature),
                new OwnerReferenceRequest(Guid.NewGuid(), OwnerType.Creature),
                []
            ),
            routeValues: new { playerId = _fromCreature.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_MovesItem_WhenLootingFromNearbyContainer()
    {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var container = Builders.MakeContainer(_worldId, LocationId);
        var item = Builders.MakeWeapon(_worldId);
        item.Quantity = 1;
        item.Ownership.OwnerId = container.Id;
        item.Ownership.OwnerType = OwnerType.Container;
        context.Props.Add(container);
        context.Items.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await _client.PostAsJsonAsync(
            "TransferInventory",
            new InventoryTransferRequest(
                new OwnerReferenceRequest(container.Id, OwnerType.Container),
                new OwnerReferenceRequest(_toCreature.Id, OwnerType.Creature),
                [new ItemSelection(item.Id, 1)]
            ),
            routeValues: new { playerId = _toCreature.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var movedItem = await verifyContext.Items.SingleAsync(
            i => i.Id == item.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(_toCreature.Id, movedItem.Ownership.OwnerId);
        Assert.Equal(OwnerType.Creature, movedItem.Ownership.OwnerType);
    }

    [Fact]
    public async Task Transfer_ReturnsBadRequest_WhenContainerIsNotNearby()
    {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var farContainer = Builders.MakeContainer(_worldId, Guid.NewGuid());
        context.Props.Add(farContainer);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await _client.PostAsJsonAsync(
            "TransferInventory",
            new InventoryTransferRequest(
                new OwnerReferenceRequest(farContainer.Id, OwnerType.Container),
                new OwnerReferenceRequest(_toCreature.Id, OwnerType.Creature),
                []
            ),
            routeValues: new { playerId = _toCreature.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "TransferInventory",
            new InventoryTransferRequest(
                new OwnerReferenceRequest(Guid.NewGuid(), OwnerType.Container),
                new OwnerReferenceRequest(_toCreature.Id, OwnerType.Creature),
                []
            ),
            routeValues: new { playerId = _toCreature.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_ReturnsBadRequest_WhenNeitherSideIsTaggedAsThePlayer()
    {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var containerA = Builders.MakeContainer(_worldId, LocationId);
        var containerB = Builders.MakeContainer(_worldId, LocationId);
        context.Props.AddRange(containerA, containerB);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act: a container-to-container transfer never involves the routed player.
        var response = await _client.PostAsJsonAsync(
            "TransferInventory",
            new InventoryTransferRequest(
                new OwnerReferenceRequest(containerA.Id, OwnerType.Container),
                new OwnerReferenceRequest(containerB.Id, OwnerType.Container),
                []
            ),
            routeValues: new { playerId = _toCreature.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetContainerInventory_ReturnsItemsOwnedByContainer()
    {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var container = Builders.MakeContainer(_worldId, LocationId);
        var item = Builders.MakeWeapon(_worldId);
        item.Quantity = 1;
        item.Ownership.OwnerId = container.Id;
        item.Ownership.OwnerType = OwnerType.Container;
        context.Props.Add(container);
        context.Items.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await _client.GetAsync(
            "GetContainerInventory",
            new { containerId = container.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InventorySummary>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        var itemDetail = Assert.Single(result.Items);
        Assert.Equal(item.Id, itemDetail.ItemId);
    }
}
