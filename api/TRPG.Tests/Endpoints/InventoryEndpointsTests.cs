using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Serialization;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
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
            routeValues: new { playerId = _fromCreature.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await _client.ReadContentFromJsonAsync<InventoryTransferResponse>(
            response,
            TestContext.Current.CancellationToken
        );
        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var movedItem = await verifyContext.Items.SingleAsync(
            i => i.Id == item.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.Null(result.TheftEncounterId);
        Assert.Equal(_toCreature.Id, movedItem.Ownership.OwnerId);
    }

    [Fact]
    public async Task Transfer_ReturnsBadRequest_WhenDestinationCarryingCapacityExceeded()
    {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var item = Builders.MakeWeapon(_worldId, weight: _toCreature.CarryingCapacity + 1);
        item.Quantity = 1;
        item.Ownership.OwnerId = _fromCreature.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        context.Items.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await _client.PostAsJsonAsync(
            "TransferInventory",
            new InventoryTransferRequest(
                new OwnerReferenceRequest(_fromCreature.Id, OwnerType.Creature),
                new OwnerReferenceRequest(_toCreature.Id, OwnerType.Creature),
                [new ItemSelection(item.Id, 1)]
            ),
            routeValues: new { playerId = _fromCreature.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DropInventoryItem_DeletesItem_FromPlayerInventory()
    {
        // Arrange
        var item = await SeedItemOnFromCreature();

        // Act
        var response = await _client.PostAsJsonAsync(
            "DropInventoryItem",
            new DropInventoryItemRequest(1),
            routeValues: new { playerId = _fromCreature.Id, itemId = item.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        Assert.False(
            await verifyContext.Items.AnyAsync(
                i => i.Id == item.Id,
                TestContext.Current.CancellationToken
            )
        );
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await _client.ReadContentFromJsonAsync<InventoryTransferResponse>(
            response,
            TestContext.Current.CancellationToken
        );
        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var movedItem = await verifyContext.Items.SingleAsync(
            i => i.Id == item.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.Null(result.TheftEncounterId);
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
    public async Task Transfer_ReturnsTheftEncounterId_WhenTheftIsCaught()
    {
        // Arrange
        var owner = Builders.MakeCreature(_worldId, locationId: LocationId);
        var item = Builders.MakeWeapon(_worldId);
        item.Quantity = 1;
        item.Ownership.OwnerId = owner.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            context.Creatures.Add(owner);
            context.Items.Add(item);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var caughtTheftFactory = CreateCaughtTheftFactory();
        using var caughtTheftClient = caughtTheftFactory.CreateClient();

        // Act
        var response = await caughtTheftClient.PostAsJsonAsync(
            $"/players/{_toCreature.Id}/inventory-transfers",
            new InventoryTransferRequest(
                new OwnerReferenceRequest(owner.Id, OwnerType.Creature),
                new OwnerReferenceRequest(_toCreature.Id, OwnerType.Creature),
                [new ItemSelection(item.Id, 1)]
            ),
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InventoryTransferResponse>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.NotNull(result.TheftEncounterId);
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

    [Fact]
    public async Task GetTheftDetectionChance_ReturnsSuccessChance_ForAnExistingCreatureSource()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "GetTheftDetectionChance",
            new TheftDetectionChanceRequest(
                new OwnerReferenceRequest(_fromCreature.Id, OwnerType.Creature),
                [new ItemSelection(Guid.NewGuid(), 1)]
            ),
            routeValues: new { playerId = _toCreature.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await _client.ReadContentFromJsonAsync<TheftDetectionChanceResponse>(
            response,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.Equal(0.45f, result.SuccessChance);
    }

    [Fact]
    public async Task GetTheftDetectionChance_ReturnsNotFound_WhenSourceDoesNotExist()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "GetTheftDetectionChance",
            new TheftDetectionChanceRequest(
                new OwnerReferenceRequest(Guid.NewGuid(), OwnerType.Creature),
                [new ItemSelection(Guid.NewGuid(), 1)]
            ),
            routeValues: new { playerId = _toCreature.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateCaughtTheftFactory()
    {
        using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var connectionString = context.Database.GetConnectionString();

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:Trpg"] = connectionString,
                        }
                    )
            );
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<TrpgDbContext>>();
                services.AddDbContext<TrpgDbContext>(options =>
                    options.UseNpgsql(connectionString)
                );
                services.RemoveAll<IChatClient>();
                services.AddKeyedSingleton<IChatClient>(
                    LlmRoleKeys.WorldGeneration,
                    fixture.ChatClient
                );
                services.AddKeyedSingleton<IChatClient>(
                    LlmRoleKeys.Gameplay,
                    (_, _) => fixture.ChatClient.AsBuilder().UseFunctionInvocation().Build()
                );
                services.RemoveAll<IChanceRoller>();
                services.AddSingleton<IChanceRoller, AlwaysSuccessfulChanceRoller>();
            });
        });
    }

    private sealed class AlwaysSuccessfulChanceRoller : IChanceRoller
    {
        public bool Roll(float chance) => true;
    }
}
