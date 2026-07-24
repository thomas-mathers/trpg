using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Contracts.GameSessions.Responses;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.GameSessions.Requests;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Hubs;

[Collection("Endpoints")]
public sealed class ChatHubTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _worldId;
    private Guid _playerId;
    private Guid _stateId;
    private Guid _districtId;

    public async ValueTask InitializeAsync()
    {
        _client = fixture.CreateClient();

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var world = Builders.MakeWorld();
        var country = Builders.MakeCountry(world.Id);
        var state = Builders.MakeState(country.Id, world.Id);
        var city = Builders.MakeCity(state.Id, country.Id, worldId: world.Id);
        var district = Builders.MakeDistrict(city.Id, worldId: world.Id);
        var player = Builders.MakeCreature(
            world.Id,
            stateId: state.Id,
            cityId: city.Id,
            districtId: district.Id
        );
        world.PlayerId = player.Id;

        context.Worlds.Add(world);
        context.Countries.Add(country);
        context.States.Add(state);
        context.Cities.Add(city);
        context.Districts.Add(district);
        context.Creatures.Add(player);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _worldId = world.Id;
        _playerId = player.Id;
        _stateId = state.Id;
        _districtId = district.Id;
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        fixture.ChatClient.PendingToolCallName = null;
        fixture.ChatClient.PendingToolCallArguments = null;
        fixture.ChatClient.ChatResponseText = "You look around. What do you want to do next?";
        return ValueTask.CompletedTask;
    }

    private async Task<Guid> StartSession()
    {
        var response = await _client.PostAsync(
            new Uri($"/sessions?worldId={_worldId}", UriKind.Relative),
            null,
            TestContext.Current.CancellationToken
        );
        var result = await response.Content.ReadFromJsonAsync<CreateSessionResponse>(
            TestContext.Current.CancellationToken
        );
        return result!.SessionId;
    }

    private async Task<Creature> SeedHostileCreature()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var creature = Builders.MakeCreature(
            _worldId,
            name: "Wraith",
            creatureType: CreatureType.Beast,
            stateId: _stateId,
            districtId: _districtId
        );
        context.Creatures.Add(creature);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }

    private Task<HttpResponseMessage> SendAdminChat(Guid sessionId, string message) =>
        _client.PostAsJsonAsync(
            new Uri($"/admin/sessions/{sessionId}/chat", UriKind.Relative),
            new ChatRequest(message),
            TestContext.Current.CancellationToken
        );

    private async Task StartFight(Guid sessionId, Creature enemy)
    {
        fixture.ChatClient.PendingToolCallName = "attack";
        fixture.ChatClient.PendingToolCallArguments = new Dictionary<string, object?>
        {
            ["abilityName"] = "Strike",
            ["targetName"] = enemy.Name,
        };
        await SendAdminChat(sessionId, $"I attack {enemy.Name}");
        fixture.ChatClient.PendingToolCallName = null;
        fixture.ChatClient.PendingToolCallArguments = null;
    }

    private static async Task<string> Drain(IAsyncEnumerable<string> tokens)
    {
        var builder = new StringBuilder();
        await foreach (var token in tokens)
        {
            builder.Append(token);
        }
        return builder.ToString();
    }

    private async Task<ChatMessage> GetChatMessage(Guid sessionId, string role)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        return await context.ChatMessages.SingleAsync(
            m => m.SessionId == sessionId && m.Role == role,
            TestContext.Current.CancellationToken
        );
    }

    private async Task<GameSession> GetGameSession(Guid sessionId)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        return await context.GameSessions.SingleAsync(
            s => s.Id == sessionId,
            TestContext.Current.CancellationToken
        );
    }

    private async Task<Fight> GetFight()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        return await context.Fights.SingleAsync(
            f => f.PlayerId == _playerId,
            TestContext.Current.CancellationToken
        );
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

    [Fact]
    public async Task ReceiveOpening_NarratesTheOpeningScene_AndPersistsTheReply()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var connection = fixture.CreateHubConnection(sessionId);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var narration = await Drain(
            connection.StreamAsync<string>("ReceiveOpening", TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal(fixture.ChatClient.ChatResponseText, narration);
        var persisted = await GetChatMessage(sessionId, "assistant");
        Assert.Contains(
            fixture.ChatClient.ChatResponseText,
            persisted.MessageJson,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task SendWait_AdvancesTimeAndNarrates()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var connection = fixture.CreateHubConnection(sessionId);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var narration = await Drain(
            connection.StreamAsync<string>("SendWait", 3, TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal(fixture.ChatClient.ChatResponseText, narration);
        var session = await GetGameSession(sessionId);
        Assert.True(session.Playtime > TimeSpan.Zero);
    }

    [Fact]
    public async Task SendChat_PersistsTheUserMessage_AndNarratesTheReply()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var connection = fixture.CreateHubConnection(sessionId);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var narration = await Drain(
            connection.StreamAsync<string>(
                "SendChat",
                "I look around.",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal(fixture.ChatClient.ChatResponseText, narration);
        var userMessage = await GetChatMessage(sessionId, "user");
        Assert.Contains("I look around.", userMessage.MessageJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendCombatAction_ResolvesTheAttack_AndNarratesTheOutcome()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();
        await StartFight(sessionId, enemy);
        await using var connection = fixture.CreateHubConnection(sessionId);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var narration = await Drain(
            connection.StreamAsync<string>(
                "SendCombatAction",
                enemy.Id,
                "Strike",
                TestContext.Current.CancellationToken
            )
        );

        // Assert — hit/miss is the only random part here (unarmed damage is a deterministic
        // 3, both combatants default to 35 max HP), so Outcome staying Ongoing is guaranteed
        // regardless of which side's roll lands; only the exact HP change is left unasserted
        Assert.Equal(fixture.ChatClient.ChatResponseText, narration);
        var fight = await GetFight();
        Assert.Equal(CombatOutcome.Ongoing, fight.Outcome);
    }

    [Fact]
    public async Task SendCombatAction_ReturnsAMessage_WhenNoFightIsActive()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var connection = fixture.CreateHubConnection(sessionId);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var narration = await Drain(
            connection.StreamAsync<string>(
                "SendCombatAction",
                Guid.NewGuid(),
                "Strike",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal("There's no fight to act in right now.", narration);
    }

    [Fact]
    public async Task SendCombatAction_ReturnsTheRejectionReason_WhenActionIsInvalid()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();
        await StartFight(sessionId, enemy);
        await using var connection = fixture.CreateHubConnection(sessionId);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var narration = await Drain(
            connection.StreamAsync<string>(
                "SendCombatAction",
                enemy.Id,
                "Nonexistent Move",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal("Item Nonexistent Move not found", narration);
    }

    [Fact]
    public async Task SendFlee_EndsTheFight_AndNarratesTheEscape()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();
        await StartFight(sessionId, enemy);
        await using var connection = fixture.CreateHubConnection(sessionId);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var narration = await Drain(
            connection.StreamAsync<string>("SendFlee", TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal(fixture.ChatClient.ChatResponseText, narration);
        var fight = await GetFight();
        Assert.Equal(CombatOutcome.Fled, fight.Outcome);
        Assert.NotNull(fight.CompletedAt);
    }

    [Fact]
    public async Task SendFlee_ReturnsAMessage_WhenNoFightIsActive()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var connection = fixture.CreateHubConnection(sessionId);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var narration = await Drain(
            connection.StreamAsync<string>("SendFlee", TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal("There's no fight to flee from right now.", narration);
    }
}
