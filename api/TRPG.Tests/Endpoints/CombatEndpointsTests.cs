using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.GameSessions.Responses;
using TRPG.Tests.Helpers;
using DataCreatureType = TRPG.Domain.Models.CreatureType;

namespace TRPG.Tests.Endpoints;

[Collection("Endpoints")]
public sealed class CombatEndpointsTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private TestApiClient _client = null!;
    private Guid _worldId;
    private Guid _playerId;
    private Guid _stateId;
    private Guid _locationId;

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
        _playerId = player.Id;
        _stateId = state.Id;
        _locationId = location.Id;
    }

    public ValueTask DisposeAsync()
    {
        fixture.ChatClient.PendingToolCallName = null;
        fixture.ChatClient.PendingToolCallArguments = null;
        fixture.ChatClient.ChatResponseText = "You look around. What do you want to do next?";
        return ValueTask.CompletedTask;
    }

    private async Task<Guid> StartSession()
    {
        var response = await _client.PostAsync(
            "CreateSession",
            body: new { WorldId = _worldId },
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
            creatureType: DataCreatureType.Beast,
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

    private async Task ResolveFleeDirectly(Guid sessionId)
    {
        await using var scope = fixture.CreateScope();
        var combatantLoader =
            scope.ServiceProvider.GetRequiredService<ActiveFightCombatantLoader>();
        var combatEngine = scope.ServiceProvider.GetRequiredService<CombatEngine>();
        var resolveCombatRound =
            scope.ServiceProvider.GetRequiredService<ResolveCombatRoundCommandHandler>();

        var combatants = await combatantLoader.Load(
            _playerId,
            TestContext.Current.CancellationToken
        );
        var state = combatEngine.ResolveFlee(combatants);

        await resolveCombatRound.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = sessionId,
                WorldId = _worldId,
                PlayerId = _playerId,
                Combatants = combatants,
                State = state,
            },
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task Attack_ReturnsOk_WhenHostileCreatureNearby()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();

        fixture.ChatClient.PendingToolCallName = "attack";
        fixture.ChatClient.PendingToolCallArguments = new Dictionary<string, object?>
        {
            ["abilityName"] = "Strike",
            ["targetName"] = enemy.Name,
        };

        // Act
        var narration = await SendChat(sessionId, $"I attack {enemy.Name}");

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(narration));
    }

    [Fact]
    public async Task Attack_ReturnsOk_WhenNothingNearbyToAttack()
    {
        // Arrange
        var sessionId = await StartSession();

        fixture.ChatClient.PendingToolCallName = "attack";
        fixture.ChatClient.PendingToolCallArguments = new Dictionary<string, object?>
        {
            ["abilityName"] = "Strike",
            ["targetName"] = "Anything",
        };

        // Act
        var narration = await SendChat(sessionId, "I attack the nearest enemy");

        // Assert — the tool has nothing to fight; this must be a graceful error, not a crash
        Assert.False(string.IsNullOrWhiteSpace(narration));
    }

    [Fact]
    public async Task Attack_ReturnsOk_WhenTargetNameDoesNotMatchAnyoneNearby()
    {
        // Arrange
        await SeedHostileCreature();
        var sessionId = await StartSession();

        fixture.ChatClient.PendingToolCallName = "attack";
        fixture.ChatClient.PendingToolCallArguments = new Dictionary<string, object?>
        {
            ["abilityName"] = "Strike",
            ["targetName"] = "Someone Who Isn't There",
        };

        // Act
        var narration = await SendChat(sessionId, "I attack someone who isn't there");

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(narration));
    }

    [Fact]
    public async Task Attack_ReturnsOk_WhenAbilityDoesNotExist()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();

        fixture.ChatClient.PendingToolCallName = "attack";
        fixture.ChatClient.PendingToolCallArguments = new Dictionary<string, object?>
        {
            ["abilityName"] = "Not A Real Ability",
            ["targetName"] = enemy.Name,
        };

        // Act — exercises CombatEngine's ability-not-found validation, not just the early return paths
        var narration = await SendChat(sessionId, $"I use a fake ability on {enemy.Name}");

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(narration));
    }

    [Fact]
    public async Task Attack_ReturnsOk_WhenAlreadyInCombat()
    {
        // Arrange — start a fight, then call the tool again as if the LLM tried to continue it
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();

        fixture.ChatClient.PendingToolCallName = "attack";
        fixture.ChatClient.PendingToolCallArguments = new Dictionary<string, object?>
        {
            ["abilityName"] = "Strike",
            ["targetName"] = enemy.Name,
        };
        await SendChat(sessionId, $"I attack {enemy.Name}");

        // Act — the tool must refuse rather than silently resolving another round outside the menu
        var narration = await SendChat(sessionId, $"I attack {enemy.Name} again");

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(narration));
    }

    [Fact]
    public async Task GetFight_ReturnsNotFound_WhenNoActiveFight()
    {
        // Act
        var response = await _client.GetAsync(
            "GetPlayerFight",
            new { playerId = _playerId },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetFight_ReturnsCombatants_WhenCombatIsActive()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();

        fixture.ChatClient.PendingToolCallName = "attack";
        fixture.ChatClient.PendingToolCallArguments = new Dictionary<string, object?>
        {
            ["abilityName"] = "Strike",
            ["targetName"] = enemy.Name,
        };
        await SendChat(sessionId, $"I attack {enemy.Name}");

        // Act
        var response = await _client.GetAsync(
            "GetPlayerFight",
            new { playerId = _playerId },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var combatants = await response.Content.ReadFromJsonAsync<
            IReadOnlyCollection<TRPG.Combat.Responses.CombatantState>
        >(TestContext.Current.CancellationToken);
        Assert.NotNull(combatants);
        Assert.Contains(combatants, c => c.IsPlayer);
        Assert.Contains(combatants, c => c.Name == enemy.Name);
    }

    [Fact]
    public async Task GetFight_ReturnsNotFound_AfterCombatEnds()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();

        fixture.ChatClient.PendingToolCallName = "attack";
        fixture.ChatClient.PendingToolCallArguments = new Dictionary<string, object?>
        {
            ["abilityName"] = "Strike",
            ["targetName"] = enemy.Name,
        };
        await SendChat(sessionId, $"I attack {enemy.Name}");

        await ResolveFleeDirectly(sessionId);

        // Act — the fight row still exists (Fled), it just isn't Ongoing anymore
        var response = await _client.GetAsync(
            "GetPlayerFight",
            new { playerId = _playerId },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
