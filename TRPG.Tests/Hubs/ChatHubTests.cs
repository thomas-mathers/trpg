using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Contracts;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Hubs;

[Collection("Endpoints")]
public sealed class ChatHubTests(EndpointTestFixture fixture) : IAsyncLifetime
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
    public async Task Connect_Succeeds_WhenNoOtherConnectionIsActiveForTheWorld()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var connection = fixture.CreateHubConnection(sessionId);

        // Act & Assert
        await connection.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task Connect_ClosesTheConnection_WhenAnotherConnectionIsAlreadyActiveForTheSameWorld()
    {
        // Arrange
        var firstSessionId = await StartSession();
        var secondSessionId = await StartSession();

        await using var firstConnection = fixture.CreateHubConnection(firstSessionId);
        await firstConnection.StartAsync(TestContext.Current.CancellationToken);

        await using var secondConnection = fixture.CreateHubConnection(secondSessionId);
        var closedTcs = new TaskCompletionSource<Exception?>();
        secondConnection.Closed += exception =>
        {
            closedTcs.TrySetResult(exception);
            return Task.CompletedTask;
        };

        // Act
        await secondConnection.StartAsync(TestContext.Current.CancellationToken);
        var closeException = await closedTcs.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(closeException);
        Assert.Contains(
            "Another connection is already active",
            closeException.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Connect_Succeeds_AfterThePriorConnectionForTheWorldHasDisconnected()
    {
        // Arrange
        var firstSessionId = await StartSession();
        var firstConnection = fixture.CreateHubConnection(firstSessionId);
        await firstConnection.StartAsync(TestContext.Current.CancellationToken);
        await firstConnection.DisposeAsync();

        var secondSessionId = await StartSession();
        await using var secondConnection = fixture.CreateHubConnection(secondSessionId);

        // Act & Assert
        await secondConnection.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HubConnectionState.Connected, secondConnection.State);
    }
}
