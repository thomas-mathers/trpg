using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Contracts;
using TRPG.Contracts.Abilities.Responses;
using TRPG.Contracts.Creatures.Requests;
using TRPG.Contracts.Creatures.Responses;
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
                Resource = Data.Models.ResourceType.Hp,
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

    [Fact]
    public async Task GetAttributePoints_ReturnsUnallocatedPoints()
    {
        // Arrange — 7 base stats at 1 each; compare against whatever BaseAttributes/PointsPerLevel
        // the app has configured, not a hardcoded literal, so tuning those doesn't break this test
        int expectedUnallocated;
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            var creature = await context.Creatures.FirstAsync(
                c => c.Id == _creature.Id,
                TestContext.Current.CancellationToken
            );
            creature.Level = 1;
            creature.BaseAttributes = new Attributes
            {
                Strength = 1,
                Defense = 1,
                Dexterity = 1,
                Endurance = 1,
                Stamina = 1,
                Mana = 1,
                Intelligence = 1,
            };
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var options = scope
                .ServiceProvider.GetRequiredService<IOptionsSnapshot<CreatureGeneratorOptions>>()
                .Value;
            expectedUnallocated =
                options.BaseAttributes.Total() + creature.Level * options.PointsPerLevel - 7;
        }

        // Act
        var response = await _client.GetAsync(
            new Uri($"/creatures/{_creature.Id}/attribute-points", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AttributePointsResponse>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.Equal(expectedUnallocated, result.UnallocatedPoints);
    }

    [Fact]
    public async Task AllocateAttributePoints_UpdatesBaseAttributes()
    {
        // Arrange
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            var creature = await context.Creatures.FirstAsync(
                c => c.Id == _creature.Id,
                TestContext.Current.CancellationToken
            );
            creature.Level = 1;
            creature.BaseAttributes = new Attributes
            {
                Strength = 1,
                Defense = 1,
                Dexterity = 1,
                Endurance = 1,
                Stamina = 1,
                Mana = 1,
                Intelligence = 1,
            };
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await _client.PostAsJsonAsync(
            new Uri($"/creatures/{_creature.Id}/attribute-points/allocate", UriKind.Relative),
            new AllocateAttributePointsRequest(
                new Dictionary<AllocatableAttributeName, int>
                {
                    [AllocatableAttributeName.Strength] = 3,
                }
            ),
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var updated = await verifyContext.Creatures.FirstAsync(
            c => c.Id == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(4, updated.BaseAttributes.Strength);
    }

    [Fact]
    public async Task GetBaseAttributes_ReturnsCreatureBaseAttributes()
    {
        // Arrange
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            var creature = await context.Creatures.FirstAsync(
                c => c.Id == _creature.Id,
                TestContext.Current.CancellationToken
            );
            creature.BaseAttributes = new Attributes
            {
                Strength = 3,
                Defense = 4,
                Dexterity = 5,
                Endurance = 6,
                Stamina = 7,
                Mana = 8,
                Intelligence = 9,
            };
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await _client.GetAsync(
            new Uri($"/creatures/{_creature.Id}/attributes", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BaseAttributesResponse>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.Equal(3, result.Strength);
        Assert.Equal(4, result.Defense);
        Assert.Equal(5, result.Dexterity);
        Assert.Equal(6, result.Endurance);
        Assert.Equal(7, result.Stamina);
        Assert.Equal(8, result.Mana);
        Assert.Equal(9, result.Intelligence);
    }

    [Fact]
    public async Task GetSkills_ReturnsExperienceProgress_ForEachLearnedSkill()
    {
        // Arrange — XpForSkillLevel(2) = 150, XpForSkillLevel(3) = 307, so experience 300
        // sits at Current = 150, ToNextLevel = 157.
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            context.CreatureSkills.Add(
                new CreatureSkill
                {
                    WorldId = _worldId,
                    CreatureId = _creature.Id,
                    Skill = Data.Models.Skill.Melee,
                    Level = 2,
                    Experience = 300,
                }
            );
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await _client.GetAsync(
            new Uri($"/creatures/{_creature.Id}/skills", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var skills = await response.Content.ReadFromJsonAsync<List<SkillProgressSummary>>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(skills);
        var skill = Assert.Single(skills);
        Assert.Equal(2, skill.Level);
        Assert.Equal(150, skill.ExperienceCurrent);
        Assert.Equal(157, skill.ExperienceToNextLevel);
    }

    [Fact]
    public async Task GetLevel_ReturnsCreatureLevel()
    {
        // Arrange
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            var creature = await context.Creatures.FirstAsync(
                c => c.Id == _creature.Id,
                TestContext.Current.CancellationToken
            );
            creature.Level = 7;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await _client.GetAsync(
            new Uri($"/creatures/{_creature.Id}/level", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreatureLevelResponse>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.Equal(7, result.Level);
    }
}
