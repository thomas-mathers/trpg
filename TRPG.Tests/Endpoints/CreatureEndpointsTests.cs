using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Contracts;
using TRPG.Contracts.Abilities.Responses;
using TRPG.Contracts.Inventory.Responses;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Endpoints;

[Collection("Endpoints")]
public sealed class CreatureEndpointsTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _worldId;
    private Creature _creature = null!;

    public async ValueTask InitializeAsync()
    {
        _client = fixture.CreateClient();

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var world = Builders.MakeWorld();
        _creature = Builders.MakeCreature(world.Id);

        context.Worlds.Add(world);
        context.Creatures.Add(_creature);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _worldId = world.Id;
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetAbilities_ReturnsStrikePlusLearnedAbilities()
    {
        // Arrange
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            context.CreatureAbilities.Add(
                new CreatureAbility
                {
                    WorldId = _worldId,
                    CreatureId = _creature.Id,
                    AbilityName = "Slash",
                }
            );
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await _client.GetAsync(
            new Uri($"/creatures/{_creature.Id}/abilities", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var abilities = await response.Content.ReadFromJsonAsync<List<AbilitySummary>>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(abilities);
        Assert.Contains(abilities, a => a.Name == "Strike");
        Assert.Contains(abilities, a => a.Name == "Slash");
    }

    [Fact]
    public async Task GetAbilities_ReturnsStrikeAndBlockOnly_ForUnknownCreatureId()
    {
        // Act — no existence check by design; an unknown creature id just has no learned abilities
        var response = await _client.GetAsync(
            new Uri($"/creatures/{Guid.NewGuid()}/abilities", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var abilities = await response.Content.ReadFromJsonAsync<List<AbilitySummary>>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(abilities);
        Assert.Contains(abilities, a => a.Name == "Strike");
        Assert.Contains(abilities, a => a.Name == "Block");
        Assert.Equal(2, abilities.Count);
    }

    [Fact]
    public async Task GetUsableItems_ReturnsConsumablesFromInventory()
    {
        // Arrange
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            var potion = new ConsumableItem
            {
                WorldId = _worldId,
                Name = "Health Potion",
                Description = "",
                Resource = TRPG.Data.Models.ResourceType.Hp,
                Amount = 50,
            };
            context.Items.Add(potion);
            context.InventoryItems.Add(
                new InventoryItem
                {
                    WorldId = _worldId,
                    CreatureId = _creature.Id,
                    ItemId = potion.Id,
                    Quantity = 1,
                    Index = 0,
                }
            );
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await _client.GetAsync(
            new Uri($"/creatures/{_creature.Id}/items", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<UsableItemSummary>>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(items);
        Assert.Contains(items, i => i.Name == "Health Potion");
    }

    [Fact]
    public async Task GetUsableItems_ReturnsEmpty_ForUnknownCreatureId()
    {
        // Act — no existence check by design; an unknown creature id just has no inventory
        var response = await _client.GetAsync(
            new Uri($"/creatures/{Guid.NewGuid()}/items", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<UsableItemSummary>>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(items);
        Assert.Empty(items);
    }
}
