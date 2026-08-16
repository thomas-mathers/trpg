using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Narration.Queries;
using TRPG.Application.Worlds;
using TRPG.Contracts;
using TRPG.Data;
using TRPG.Jobs.Responses;
using TRPG.Tests.Helpers;
using TRPG.Worlds.Requests;
using TRPG.Worlds.Responses;
using DataCreatureType = TRPG.Domain.Models.CreatureType;
using DataSkill = TRPG.Domain.Models.Skill;
using OwnerType = TRPG.Domain.Models.OwnerType;

namespace TRPG.Tests.Endpoints;

[Collection("Endpoints")]
public sealed class WorldEndpointsTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private TestApiClient _client = null!;

    public ValueTask InitializeAsync()
    {
        _client = fixture.CreateApiClient();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task CreateWorld_ReturnsWorldAndPlayer_WhenRequestIsMinimal()
    {
        // Arrange
        var request = new CreateWorldRequest
        {
            PlayerName = "Test Player",
            Gender = PlayerGender.Male,
            Age = 18,
            Race = Race.Human,
            PlayerClass = PlayerClass.Knight,
            MinCityStates = 1,
            MaxCityStates = 1,
            MinRuralStates = 5,
            MaxRuralStates = 5,
            MinBuildingsPerState = 0,
            MaxBuildingsPerState = 0,
            MinFactionMembers = 1,
            MaxFactionMembers = 1,
            FactionCount = 1,
            HousesPerCity = 1,
            MinHouseholdSize = 1,
            MaxHouseholdSize = 1,
        };

        // Act
        var response = await _client.SendAsync(
            "CreateWorld",
            body: request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var enqueued = await response.Content.ReadFromJsonAsync<EnqueueJobResponse>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(enqueued);
        Assert.NotEqual(Guid.Empty, enqueued.JobId);

        var jobStatus = await WaitForJobCompletion(enqueued.JobId);
        Assert.Equal(JobStatus.Done, jobStatus.Status);

        var result = JsonSerializer.Deserialize<CreateWorldResponse>(
            jobStatus.ResultJson!,
            TrpgJsonOptions.Default
        );
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.WorldId);
        Assert.NotEqual(Guid.Empty, result.PlayerId);
        Assert.False(string.IsNullOrWhiteSpace(result.WorldName));
    }

    [Fact]
    public async Task CreateWorld_PersistsMonsterSkillsAbilitiesAndGear_WhenDungeonsAreGenerated()
    {
        // Arrange — one dungeon per state guarantees monsters exist in the generated world
        var request = new CreateWorldRequest
        {
            PlayerName = "Test Player",
            Gender = PlayerGender.Male,
            Age = 18,
            Race = Race.Human,
            PlayerClass = PlayerClass.Knight,
            MinCityStates = 1,
            MaxCityStates = 1,
            MinRuralStates = 5,
            MaxRuralStates = 5,
            MinBuildingsPerState = 1,
            MaxBuildingsPerState = 1,
            MinFactionMembers = 1,
            MaxFactionMembers = 1,
            FactionCount = 1,
            HousesPerCity = 1,
            MinHouseholdSize = 1,
            MaxHouseholdSize = 1,
        };

        // Act
        var response = await _client.SendAsync(
            "CreateWorld",
            body: request,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var enqueued = await response.Content.ReadFromJsonAsync<EnqueueJobResponse>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        var jobStatus = await WaitForJobCompletion(enqueued!.JobId);
        Assert.Equal(JobStatus.Done, jobStatus.Status);
        var result = JsonSerializer.Deserialize<CreateWorldResponse>(
            jobStatus.ResultJson!,
            TrpgJsonOptions.Default
        )!;

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var monsters = await context
            .Creatures.Where(c => c.WorldId == result.WorldId && c.Profession == null)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(monsters);

        var monsterIds = monsters.Select(m => m.Id).ToArray();
        var skillsByCreature = (
            await context
                .CreatureSkills.Where(s => monsterIds.Contains(s.CreatureId))
                .ToListAsync(TestContext.Current.CancellationToken)
        ).ToLookup(s => s.CreatureId);
        var inventoryByCreature = (
            await context
                .Items.Where(i =>
                    i.Ownership.OwnerType == OwnerType.Creature
                    && monsterIds.Contains(i.Ownership.OwnerId)
                )
                .ToListAsync(TestContext.Current.CancellationToken)
        ).ToLookup(i => i.Ownership.OwnerId);

        Assert.All(
            monsters,
            m => Assert.Equal(Enum.GetValues<DataSkill>().Length, skillsByCreature[m.Id].Count())
        );
        var armedCreatureTypes = new[]
        {
            DataCreatureType.Undead,
            DataCreatureType.Demon,
            DataCreatureType.Goblin,
        };
        Assert.All(
            monsters.Where(m => armedCreatureTypes.Contains(m.CreatureType)),
            m => Assert.NotEmpty(inventoryByCreature[m.Id])
        );
    }

    private async Task<JobStatusResponse> WaitForJobCompletion(Guid jobId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var status = await _client.ReadFromJsonAsync<JobStatusResponse>(
                "GetJob",
                new { id = jobId },
                cancellationToken: TestContext.Current.CancellationToken
            );
            if (status!.Status is JobStatus.Done or JobStatus.Failed or JobStatus.Cancelled)
            {
                return status;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Job {jobId} did not complete within the test deadline.");
    }

    [Fact]
    public async Task ListWorlds_ReturnsSeededWorld_WhenWorldExists()
    {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var world = Builders.MakeWorld();
        world.PlayerId = Guid.NewGuid();
        context.Worlds.Add(world);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await _client.SendAsync(
            "ListWorlds",
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var worlds = await response.Content.ReadFromJsonAsync<List<WorldSummary>>(
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(worlds);
        Assert.Contains(worlds, w => w.WorldId == world.Id && w.Name == world.Name && w.HasPlayer);
    }

    [Fact]
    public async Task DropWorld_RemovesWorld_WhenWorldExists()
    {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var world = Builders.MakeWorld();
        context.Worlds.Add(world);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await _client.SendAsync(
            "DropWorld",
            new { worldId = world.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        Assert.Null(
            await verifyContext.Worlds.FindAsync([world.Id], TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task DropWorld_ClearsTheLoreAnchorCaches_ForTheDroppedWorld()
    {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var world = Builders.MakeWorld();
        context.Worlds.Add(world);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var loreAnchorsKey = GetLoreAnchorsByWorldQueryHandler.CacheKey(world.Id);
        var automatonKey = GetLoreAnchorAutomatonByWorldQueryHandler.CacheKey(world.Id);
        cache.Set(loreAnchorsKey, Array.Empty<object>());
        cache.Set(automatonKey, new object());

        // Act
        var response = await _client.SendAsync(
            "DropWorld",
            new { worldId = world.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(cache.TryGetValue(loreAnchorsKey, out _));
        Assert.False(cache.TryGetValue(automatonKey, out _));
    }
}
