using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Contracts;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Endpoints")]
public sealed class WorldEndpointsTests(EndpointTestFixture fixture) : IAsyncLifetime {
    private HttpClient _client = null!;

    public ValueTask InitializeAsync() {
        _client = fixture.CreateClient();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task CreateWorld_ReturnsWorldAndPlayer_WhenRequestIsMinimal() {
        // Arrange
        var request = new CreateWorldRequest {
            PlayerName = "Test Player",
            Profession = Profession.Knight,
            MinCountries = 1,
            MaxCountries = 1,
            MinCityStates = 1,
            MaxCityStates = 1,
            MinRuralStates = 0,
            MaxRuralStates = 0,
            MinBuildingsPerState = 0,
            MaxBuildingsPerState = 0,
            MinFactionMembers = 1,
            MaxFactionMembers = 1,
            FactionCount = 1,
            RaceCount = 1,
            HousesPerCity = 1,
            MinHouseholdSize = 1,
            MaxHouseholdSize = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/worlds", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateWorldResponse>(TestContext.Current
            .CancellationToken);
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.WorldId);
        Assert.NotEqual(Guid.Empty, result.PlayerId);
        Assert.False(string.IsNullOrWhiteSpace(result.WorldName));
    }

    [Fact]
    public async Task ListWorlds_ReturnsSeededWorld_WhenWorldExists() {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var world = Builders.MakeWorld();
        world.PlayerId = Guid.NewGuid();
        context.Worlds.Add(world);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await _client.GetAsync(new Uri("/worlds", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var worlds = await response.Content.ReadFromJsonAsync<List<WorldSummary>>(TestContext.Current
            .CancellationToken);
        Assert.NotNull(worlds);
        Assert.Contains(worlds, w => w.WorldId == world.Id && w.Name == world.Name && w.HasPlayer);
    }

    [Fact]
    public async Task DropWorld_RemovesWorld_WhenWorldExists() {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var world = Builders.MakeWorld();
        context.Worlds.Add(world);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await _client.DeleteAsync(new Uri($"/worlds/{world.Id}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        Assert.Null(await verifyContext.Worlds.FindAsync([world.Id], TestContext.Current.CancellationToken));
    }
}
