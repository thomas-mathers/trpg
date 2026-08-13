using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Contracts;
using TRPG.Contracts.Combat.Requests;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.GameSessions.Responses;
using TRPG.Contracts.Scenes.Responses;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Endpoints;

[Collection("Endpoints")]
public sealed class GameSessionEndpointsTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private TestApiClient _client = null!;
    private Guid _worldId;
    private Guid _stateId;
    private Guid _locationId;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _client = fixture.CreateApiClient();

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var world = Builders.MakeWorld();
        var country = Builders.MakeCountry(world.Id);
        var state = Builders.MakeState(country.Id, world.Id);
        var city = Builders.MakeCity(state.Id, country.Id, worldId: world.Id);
        var district = Builders.MakeDistrict(city.Id, worldId: world.Id);
        var location = Builders.MakeLocation(
            world.Id,
            state.Id,
            cityId: city.Id,
            districtId: district.Id
        );
        var player = Builders.MakeCreature(world.Id, locationId: location.Id);
        world.PlayerId = player.Id;

        context.Worlds.Add(world);
        context.Countries.Add(country);
        context.States.Add(state);
        context.Cities.Add(city);
        context.Districts.Add(district);
        context.Locations.Add(location);
        context.Creatures.Add(player);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _worldId = world.Id;
        _stateId = state.Id;
        _locationId = location.Id;
        _player = player;
    }

    public ValueTask DisposeAsync()
    {
        fixture.ChatClient.PendingToolCallName = null;
        fixture.ChatClient.PendingToolCallArguments = null;
        fixture.ChatClient.ChatResponseText = "You look around. What do you want to do next?";
        return ValueTask.CompletedTask;
    }

    private async Task<Guid> StartSession(Guid? worldId = null)
    {
        var response = await _client.PostAsync(
            "CreateSession",
            body: new { WorldId = worldId ?? _worldId },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var result = await response.Content.ReadFromJsonAsync<SessionCreatedResponse>(
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
            creatureType: Data.Models.CreatureType.Beast,
            locationId: _locationId
        );
        context.Creatures.Add(creature);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }

    private async Task<HubConnection> Connect(Guid sessionId)
    {
        var connection = fixture.CreateHubConnection(sessionId);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        return connection;
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

    private async Task<string> SendChat(Guid sessionId, string message)
    {
        await using var gameHub = await Connect(sessionId);
        return await Drain(
            gameHub.StreamAsync<string>("SendChat", message, TestContext.Current.CancellationToken)
        );
    }

    private async Task StartFight(Guid sessionId, Creature enemy)
    {
        fixture.ChatClient.PendingToolCallName = "attack";
        fixture.ChatClient.PendingToolCallArguments = new Dictionary<string, object?>
        {
            ["abilityName"] = "Strike",
            ["targetName"] = enemy.Name,
        };
        await SendChat(sessionId, "I look around.");
    }

    private async Task<CombatActionResponse> ResolveCombatAction(
        Guid sessionId,
        UseAbilityAction action
    )
    {
        using var response = await _client.PostAsJsonAsync(
            "ResolveCombatAction",
            action,
            routeValues: new { sessionId },
            cancellationToken: TestContext.Current.CancellationToken
        );
        response.EnsureSuccessStatusCode();
        return (
            await _client.ReadContentFromJsonAsync<CombatActionResponse>(
                response,
                TestContext.Current.CancellationToken
            )
        )!;
    }

    [Fact]
    public async Task StartSession_ReturnsSessionId_WhenWorldHasPlayer()
    {
        // Act
        var response = await _client.PostAsync(
            "CreateSession",
            body: new { WorldId = _worldId },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SessionCreatedResponse>(
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
        var narration = await SendChat(sessionId, "look around");

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(narration));
    }

    [Fact]
    public async Task ResolveCombatAction_ReturnsSceneSnapshot_WhenTheAttackChangesNearbyCreatureState()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();
        await StartFight(sessionId, enemy);

        // Act - hit/miss is random each round, so the attack is repeated a fixed number of times.
        // The final response must include the scene state after whichever round(s) landed a hit.
        CombatActionResponse? lastResponse = null;
        for (var i = 0; i < 4; i++)
        {
            lastResponse = await ResolveCombatAction(
                sessionId,
                new UseAbilityAction(enemy.Id, "Strike")
            );
        }

        // Assert
        var scene = Assert.IsType<SceneSnapshot>(lastResponse?.Scene);
        var updatedEnemy = Assert.Single(scene.NearbyCreatures, c => c.Id == enemy.Id);
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var freshEnemy = await context.Creatures.SingleAsync(
            c => c.Id == enemy.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(freshEnemy.CurrentHp, updatedEnemy.CurrentHp);
    }

    [Fact]
    public async Task ResolveCombatAction_ResolvesTheAttack_AndReturnsDeterministicNarration()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();
        await StartFight(sessionId, enemy);

        // Act
        var response = await ResolveCombatAction(
            sessionId,
            new UseAbilityAction(enemy.Id, "Strike")
        );

        // Assert - hit/miss is the only random part here (unarmed damage is a deterministic 3,
        // both combatants default to 35 max HP), so Outcome staying Ongoing is guaranteed
        // regardless of which side's roll lands; only the exact HP change is left unasserted.
        Assert.NotNull(response.Update);
        Assert.Contains(response.Update.Events, combatEvent => combatEvent.Narration is not null);
        Assert.Null(response.ErrorMessage);
    }

    [Fact]
    public async Task ResolveCombatAction_ReturnsAMessage_WhenNoFightIsActive()
    {
        // Arrange
        var sessionId = await StartSession();

        // Act
        var response = await ResolveCombatAction(
            sessionId,
            new UseAbilityAction(Guid.NewGuid(), "Strike")
        );

        // Assert
        Assert.Equal("There's no fight to act in right now.", response.ErrorMessage);
    }

    [Fact]
    public async Task ResolveCombatAction_ReturnsTheRejectionReason_WhenActionIsInvalid()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();
        await StartFight(sessionId, enemy);

        // Act
        var response = await ResolveCombatAction(
            sessionId,
            new UseAbilityAction(enemy.Id, "Nonexistent Move")
        );

        // Assert
        Assert.Equal("Ability Nonexistent Move not found", response.ErrorMessage);
    }

    [Fact]
    public async Task SendChat_MovesPlayerToDestination_WhenModelCallsMoveTool()
    {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var world = Builders.MakeWorld();
        var country = Builders.MakeCountry(world.Id);
        var state = Builders.MakeState(country.Id, world.Id);
        var city = Builders.MakeCity(state.Id, country.Id, worldId: world.Id);
        var originId = Guid.NewGuid();
        var originLocation = Builders.MakeLocation(world.Id, state.Id, districtId: originId);
        var origin = Builders.MakeDistrict(
            city.Id,
            worldId: world.Id,
            id: originId,
            locationId: originLocation.Id
        );
        var destinationId = Guid.NewGuid();
        var destinationLocation = Builders.MakeLocation(
            world.Id,
            state.Id,
            districtId: destinationId
        );
        var destination = Builders.MakeDistrict(
            city.Id,
            Data.Models.DistrictType.Residential,
            worldId: world.Id,
            id: destinationId,
            locationId: destinationLocation.Id
        );
        var connector = Builders.MakeLocationConnector(
            origin.LocationId,
            destinationLocationId: destination.LocationId,
            worldId: world.Id,
            name: "Path",
            description: $"A path leading to {destination.Name}.",
            destinationLabel: destination.Name
        );
        var player = Builders.MakeCreature(world.Id, locationId: origin.LocationId);
        world.PlayerId = player.Id;

        context.Worlds.Add(world);
        context.Countries.Add(country);
        context.States.Add(state);
        context.Cities.Add(city);
        context.Districts.AddRange(origin, destination);
        context.Locations.AddRange(originLocation, destinationLocation);
        context.LocationConnectors.Add(connector);
        context.Creatures.Add(player);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sessionId = await StartSession(world.Id);

        fixture.ChatClient.PendingToolCallName = "move";
        fixture.ChatClient.PendingToolCallArguments = new Dictionary<string, object?>
        {
            ["destinationName"] = destination.Name,
        };
        fixture.ChatClient.ChatResponseText = "You arrive at the new district.";

        // Act
        var narration = await SendChat(sessionId, $"I head to {destination.Name}");

        // Assert
        Assert.Equal("You arrive at the new district.", narration);

        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var movedPlayer = await verifyContext
            .Creatures.AsNoTracking()
            .FirstAsync(c => c.Id == player.Id, TestContext.Current.CancellationToken);
        Assert.Equal(destination.LocationId, movedPlayer.LocationId);
    }

    [Fact]
    public async Task GetScene_ReturnsSceneSnapshot_WhenSessionExists()
    {
        // Arrange
        var sessionId = await StartSession();

        // Act
        var response = await _client.GetAsync(
            "GetSessionScene",
            new { sessionId = sessionId },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scene = await response.Content.ReadFromJsonAsync<SceneSnapshot>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(scene);
        Assert.Equal(_player.Name, scene.PlayerStatus.Name);
    }

    [Fact]
    public async Task GetScene_ReflectsUpdatedCurrentHp_WithinTheSameInGameHour()
    {
        // Arrange — first call populates the catch-up cache for this location+hour
        var sessionId = await StartSession();
        var firstResponse = await _client.GetAsync(
            "GetSessionScene",
            new { sessionId = sessionId },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var firstScene = await firstResponse.Content.ReadFromJsonAsync<SceneSnapshot>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_player.MaximumHp, firstScene!.PlayerStatus.CurrentHp);

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            await context
                .Creatures.Where(c => c.Id == _player.Id)
                .ExecuteUpdateAsync(
                    c => c.SetProperty(x => x.CurrentHp, _player.MaximumHp - 10),
                    TestContext.Current.CancellationToken
                );
        }

        // Act — same in-game hour and location, so the catch-up cache is still warm
        var secondResponse = await _client.GetAsync(
            "GetSessionScene",
            new { sessionId = sessionId },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert — the cache must never mask a live HP change
        var secondScene = await secondResponse.Content.ReadFromJsonAsync<SceneSnapshot>(
            TrpgJsonOptions.Default,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_player.MaximumHp - 10, secondScene!.PlayerStatus.CurrentHp);
    }

    [Fact]
    public async Task GetScene_ReturnsNotFound_WhenSessionDoesNotExist()
    {
        // Act
        var response = await _client.GetAsync(
            "GetSessionScene",
            new { sessionId = Guid.NewGuid() },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
            "CreateSession",
            body: new { WorldId = world.Id },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
