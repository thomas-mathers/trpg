using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.GameSessions.Responses;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.GameSessions.Requests;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Endpoints;

[Collection("Endpoints")]
public sealed class CombatEndpointsTests(EndpointTestFixture fixture) : IAsyncLifetime
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
            creatureType: CreatureType.Beast,
            stateId: _stateId,
            districtId: _districtId
        );
        context.Creatures.Add(creature);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }

    private Task<HttpResponseMessage> SendChat(Guid sessionId, string message) =>
        _client.PostAsJsonAsync(
            new Uri($"/admin/sessions/{sessionId}/chat", UriKind.Relative),
            new ChatRequest(message),
            TestContext.Current.CancellationToken
        );

    private async Task ResolveFleeDirectly(Guid sessionId)
    {
        await using var scope = fixture.CreateScope();
        var getCombatants = scope.ServiceProvider.GetRequiredService<GetCombatantsQueryHandler>();
        var combatEngine = scope.ServiceProvider.GetRequiredService<CombatEngine>();
        var resolveCombatRound =
            scope.ServiceProvider.GetRequiredService<ResolveCombatRoundCommandHandler>();

        var combatants = await getCombatants.Handle(
            new GetCombatantsQuery { PlayerId = _playerId },
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
        var response = await SendChat(sessionId, $"I attack {enemy.Name}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
        var response = await SendChat(sessionId, "I attack the nearest enemy");

        // Assert — the tool has nothing to fight; this must be a graceful error, not a crash
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
        var response = await SendChat(sessionId, "I attack someone who isn't there");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
        var response = await SendChat(sessionId, $"I use a fake ability on {enemy.Name}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
        var response = await SendChat(sessionId, $"I attack {enemy.Name} again");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetFight_ReturnsNotFound_WhenNoActiveFight()
    {
        // Act
        var response = await _client.GetAsync(
            new Uri($"/players/{_playerId}/fight", UriKind.Relative),
            TestContext.Current.CancellationToken
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
            new Uri($"/players/{_playerId}/fight", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fight = await response.Content.ReadFromJsonAsync<FightState>(
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(fight);
        Assert.Contains(fight.Combatants, c => c.IsPlayer);
        Assert.Contains(fight.Combatants, c => c.Name == enemy.Name);
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
            new Uri($"/players/{_playerId}/fight", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
