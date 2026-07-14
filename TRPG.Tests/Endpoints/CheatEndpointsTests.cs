using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Contracts;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Endpoints;

[Collection("Endpoints")]
public sealed class CheatEndpointsTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _worldId;
    private Guid _playerId;

    public async ValueTask InitializeAsync()
    {
        _client = fixture.CreateClient();

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var world = Builders.MakeWorld();
        var country = Builders.MakeCountry(world.Id);
        var state = Builders.MakeState(country.Id, world.Id);
        var player = Builders.MakeCreature(world.Id, stateId: state.Id);
        world.PlayerId = player.Id;

        context.Worlds.Add(world);
        context.Countries.Add(country);
        context.States.Add(state);
        context.Creatures.Add(player);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _worldId = world.Id;
        _playerId = player.Id;
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Guid> StartSession()
    {
        var response = await _client.PostAsync(
            new Uri($"/worlds/{_worldId}/sessions", UriKind.Relative),
            null,
            TestContext.Current.CancellationToken
        );
        var result = await response.Content.ReadFromJsonAsync<CreateSessionResponse>(
            TestContext.Current.CancellationToken
        );
        return result!.SessionId;
    }

    [Fact]
    public async Task GrantAllAbilities_PersistsAbilities_WhenSessionExists()
    {
        // Arrange
        var sessionId = await StartSession();

        // Act
        var response = await _client.PostAsync(
            new Uri($"/sessions/{sessionId}/cheat/grant-all-abilities", UriKind.Relative),
            null,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var grantedCount = await context
            .CreatureAbilities.Where(a => a.CreatureId == _playerId)
            .CountAsync(TestContext.Current.CancellationToken);
        Assert.True(grantedCount > 0);
    }

    [Fact]
    public async Task GrantAllAbilities_ReturnsNotFound_WhenSessionDoesNotExist()
    {
        // Act
        var response = await _client.PostAsync(
            new Uri($"/sessions/{Guid.NewGuid()}/cheat/grant-all-abilities", UriKind.Relative),
            null,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
