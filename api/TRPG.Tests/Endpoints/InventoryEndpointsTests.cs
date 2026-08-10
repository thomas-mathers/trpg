using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Contracts;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data;
using TRPG.Data.Models;
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

        var item = Builders.MakeWeaponItem(_worldId);
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
                _fromCreature.Id,
                _toCreature.Id,
                [new ItemSelection(item.Id, 1)]
            ),
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
                _fromCreature.Id,
                farCreature.Id,
                [new ItemSelection(item.Id, 1)]
            ),
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
            new InventoryTransferRequest(Guid.NewGuid(), _toCreature.Id, []),
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
            new InventoryTransferRequest(_fromCreature.Id, Guid.NewGuid(), []),
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
