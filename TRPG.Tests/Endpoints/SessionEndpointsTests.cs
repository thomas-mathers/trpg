using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Contracts;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Endpoints;

[Collection("Endpoints")]
public sealed class SessionEndpointsTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _worldId;

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
    public async Task StartSession_ReturnsSessionId_WhenWorldHasPlayer()
    {
        // Act
        var response = await _client.PostAsync(
            new Uri($"/worlds/{_worldId}/sessions", UriKind.Relative),
            null,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateSessionResponse>(
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.SessionId);
    }

    [Fact]
    public async Task SendChat_ReturnsNarration_WhenSessionExists()
    {
        // Arrange
        var sessionId = await StartSession();

        // Act
        var response = await _client.PostAsJsonAsync(
            new Uri($"/sessions/{sessionId}/chat", UriKind.Relative),
            new ChatRequest("look around"),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Response));
        Assert.Null(result.Metrics);
    }

    [Fact]
    public async Task Wait_AdvancesTimeAndReturnsMessage_WhenSessionExists()
    {
        // Arrange
        var sessionId = await StartSession();

        // Act
        var response = await _client.PostAsJsonAsync(
            new Uri($"/sessions/{sessionId}/wait", UriKind.Relative),
            new WaitRequest(5),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WaitResponse>(
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.Contains("Time passes", result.Message, StringComparison.Ordinal);
        Assert.Contains("hour 13", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EndSession_SavesPlaytimeAndReturnsNoContent_WhenSessionExists()
    {
        // Arrange
        var sessionId = await StartSession();

        // Act
        var response = await _client.DeleteAsync(
            new Uri($"/sessions/{sessionId}", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var world = await context.Worlds.FindAsync(
            [_worldId],
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(world);
        Assert.True(world.Playtime > TimeSpan.Zero);
    }

    [Fact]
    public async Task StartSession_ReturnsNotFound_WhenWorldHasNoPlayer()
    {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var world = Builders.MakeWorld();
        context.Worlds.Add(world);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await _client.PostAsync(
            new Uri($"/worlds/{world.Id}/sessions", UriKind.Relative),
            null,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SendChat_ReturnsNotFound_WhenSessionDoesNotExist()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            new Uri($"/sessions/{Guid.NewGuid()}/chat", UriKind.Relative),
            new ChatRequest("look around"),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Wait_ReturnsNotFound_WhenSessionDoesNotExist()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            new Uri($"/sessions/{Guid.NewGuid()}/wait", UriKind.Relative),
            new WaitRequest(5),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Wait_ReturnsBadRequest_WhenHoursIsNotPositive()
    {
        // Arrange
        var sessionId = await StartSession();

        // Act
        var response = await _client.PostAsJsonAsync(
            new Uri($"/sessions/{sessionId}/wait", UriKind.Relative),
            new WaitRequest(0),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EndSession_ReturnsNotFound_WhenSessionDoesNotExist()
    {
        // Act
        var response = await _client.DeleteAsync(
            new Uri($"/sessions/{Guid.NewGuid()}", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
