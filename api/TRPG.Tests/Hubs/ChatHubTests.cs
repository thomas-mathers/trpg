using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.GameSessions.Hubs;
using TRPG.GameSessions.Responses;
using TRPG.Tests.Helpers;
using TypedSignalR.Client;
using DataCreatureType = TRPG.Domain.Models.CreatureType;
using DataDistrictType = TRPG.Domain.Models.DistrictType;

namespace TRPG.Tests.Hubs;

[Collection("Endpoints")]
public sealed class ChatHubTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan PushTimeout = TimeSpan.FromSeconds(10);

    private TestApiClient _client = null!;
    private Guid _worldId;
    private Guid _playerId;
    private Guid _stateId;
    private Guid _cityId;
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
        _cityId = city.Id;
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

    private async Task<HubConnection> Connect(Guid sessionId)
    {
        var connection = fixture.CreateHubConnection(sessionId);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private async Task<Creature> SeedHostileCreature()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var creature = Builders.MakeCreature(
            _worldId,
            name: "Wraith",
            creatureType: DataCreatureType.Beast,
            locationId: _locationId
        );
        context.Creatures.Add(creature);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }

    private async Task StartFight(Guid sessionId, Creature enemy)
    {
        fixture.ChatClient.PendingToolCallName = "attack";
        fixture.ChatClient.PendingToolCallArguments = new Dictionary<string, object?>
        {
            ["abilityName"] = "Strike",
            ["targetName"] = enemy.Name,
        };
        await using (var setupHub = await Connect(sessionId))
        {
            await Drain(
                setupHub.StreamAsync<string>(
                    "SendChat",
                    $"I attack {enemy.Name}",
                    TestContext.Current.CancellationToken
                )
            );
        }
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

    private async Task<FightEncounter> GetFight()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        return await context
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(f => f.PlayerId == _playerId, TestContext.Current.CancellationToken);
    }

    private async Task<World> GetWorld()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        return await context.Worlds.SingleAsync(
            w => w.Id == _worldId,
            TestContext.Current.CancellationToken
        );
    }

    private async Task<(TheftEncounter Encounter, Creature Owner)> SeedActiveTheftEncounter()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var owner = Builders.MakeCreature(_worldId, name: "Mara", locationId: _locationId);
        var crime = new TheftCrime
        {
            WorldId = _worldId,
            PlayerId = _playerId,
            LocationId = _locationId,
            OwnerCreatureId = owner.Id,
            OwnerName = owner.Name,
            SourceOwnerId = owner.Id,
            SourceOwnerType = OwnerType.Creature,
            Items = [new TheftCrimeItem("Silver Ring", 1)],
        };
        var encounter = new TheftEncounter
        {
            WorldId = _worldId,
            PlayerId = _playerId,
            LocationId = _locationId,
            TheftCrimeId = crime.Id,
            OwnerCreatureId = owner.Id,
            OwnerName = owner.Name,
            SourceOwnerId = owner.Id,
            SourceOwnerType = OwnerType.Creature,
            ItemNames = ["Silver Ring"],
        };
        context.Creatures.Add(owner);
        context.Crimes.Add(crime);
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (encounter, owner);
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
    public async Task Connect_PushesSceneSnapshot_WhenSessionStarts()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var connection = fixture.CreateHubConnection(sessionId);
        var snapshotReceived = new TaskCompletionSource<SceneSnapshot>();
        var gameClient = new TestGameClient
        {
            OnSceneSnapshot = snapshot => snapshotReceived.TrySetResult(snapshot),
        };
        connection.Register<IGameClient>(gameClient);

        // Act
        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var snapshot = await snapshotReceived.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_playerId, snapshot.PlayerStatus.Id);
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
    public async Task Reconnect_PushesSceneSnapshot_WhenSessionResumes()
    {
        // Arrange
        var sessionId = await StartSession();
        var firstConnection = fixture.CreateHubConnection(sessionId);
        var firstSnapshotReceived =
            new TaskCompletionSource<TRPG.GameSessions.Responses.SceneSnapshot>();
        var firstGameClient = new TestGameClient
        {
            OnSceneSnapshot = snapshot => firstSnapshotReceived.TrySetResult(snapshot),
        };
        firstConnection.Register<IGameClient>(firstGameClient);
        await firstConnection.StartAsync(TestContext.Current.CancellationToken);
        var firstSnapshot = await firstSnapshotReceived.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        await firstConnection.DisposeAsync();

        await using var secondConnection = fixture.CreateHubConnection(sessionId);
        var secondSnapshotReceived =
            new TaskCompletionSource<TRPG.GameSessions.Responses.SceneSnapshot>();
        var secondGameClient = new TestGameClient
        {
            OnSceneSnapshot = snapshot => secondSnapshotReceived.TrySetResult(snapshot),
        };
        secondConnection.Register<IGameClient>(secondGameClient);

        // Act
        await secondConnection.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var secondSnapshot = await secondSnapshotReceived.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_playerId, firstSnapshot.PlayerStatus.Id);
        Assert.Equal(_playerId, secondSnapshot.PlayerStatus.Id);
    }

    [Fact]
    public async Task ReceiveOpening_NarratesTheOpeningScene_AndPersistsTheReply()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var narration = await Drain(
            gameHub.StreamAsync<string>("ReceiveOpening", TestContext.Current.CancellationToken)
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
        await using var gameHub = await Connect(sessionId);

        // Act
        var narration = await Drain(
            gameHub.StreamAsync<string>("SendWait", 3, 0, TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal(fixture.ChatClient.ChatResponseText, narration);
        var session = await GetGameSession(sessionId);
        Assert.True(session.Playtime > TimeSpan.Zero);
    }

    [Fact]
    public async Task SendWait_AdvancesTime_WhenOnlyMinutesAreProvided()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var narration = await Drain(
            gameHub.StreamAsync<string>("SendWait", 0, 30, TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal(fixture.ChatClient.ChatResponseText, narration);
        var session = await GetGameSession(sessionId);
        Assert.True(session.Playtime > TimeSpan.Zero);
    }

    [Fact]
    public async Task SendWait_ReturnsAMessage_WhenDurationIsNotPositive()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var narration = await Drain(
            gameHub.StreamAsync<string>("SendWait", 0, 0, TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal("The wait duration must be positive.", narration);
        var session = await GetGameSession(sessionId);
        Assert.Equal(TimeSpan.Zero, session.Playtime);
    }

    [Fact]
    public async Task SendChat_PersistsTheUserMessage_AndNarratesTheReply()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var narration = await Drain(
            gameHub.StreamAsync<string>(
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
    public async Task SendChat_LinksASeededEntityMentionedInTheReply_AsEntityMarkup()
    {
        // Arrange
        var creature = await SeedHostileCreature();
        fixture.ChatClient.ChatResponseText = $"A {creature.Name} lurks in the shadows.";
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var narration = await Drain(
            gameHub.StreamAsync<string>(
                "SendChat",
                "I look around.",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal(
            $"A [{creature.Name}](entity://Creature/{creature.Id}) lurks in the shadows.",
            narration
        );
    }

    [Fact]
    public async Task SendChat_DoesNotPushSceneSnapshot_WhenNothingChangedDuringTheTurn()
    {
        // Arrange
        await SeedHostileCreature();
        var sessionId = await StartSession();
        var connection = fixture.CreateHubConnection(sessionId);
        var snapshots = new List<TRPG.GameSessions.Responses.SceneSnapshot>();
        var initialSnapshotReceived =
            new TaskCompletionSource<TRPG.GameSessions.Responses.SceneSnapshot>();
        var gameClient = new TestGameClient
        {
            OnSceneSnapshot = snapshot =>
            {
                snapshots.Add(snapshot);
                initialSnapshotReceived.TrySetResult(snapshot);
            },
        };
        connection.Register<IGameClient>(gameClient);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await initialSnapshotReceived.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        snapshots.Clear();
        await using var gameHub = connection;

        // Act
        await Drain(
            gameHub.StreamAsync<string>(
                "SendChat",
                "I look around.",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Empty(snapshots);
    }

    [Fact]
    public async Task SendFlee_EndsTheFight_AndNarratesTheEscape()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();
        await StartFight(sessionId, enemy);
        await using var gameHub = await Connect(sessionId);

        // Act
        var narration = await Drain(
            gameHub.StreamAsync<string>("SendFlee", TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal(fixture.ChatClient.ChatResponseText, narration);
        var fight = await GetFight();
        Assert.Equal(CombatOutcome.Fled, fight.Outcome);
        Assert.NotNull(fight.CompletedAt);
    }

    [Fact]
    public async Task SendFlee_PublishesCombatUpdatedWithFledOutcome_WhenFleeSucceeds()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();
        await StartFight(sessionId, enemy);
        var connection = fixture.CreateHubConnection(sessionId);
        var combatUpdatedReceived =
            new TaskCompletionSource<TRPG.Combat.Responses.CombatUpdatePayload>();
        var gameClient = new TestGameClient
        {
            OnCombatUpdated = payload => combatUpdatedReceived.TrySetResult(payload),
        };
        connection.Register<IGameClient>(gameClient);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await using var gameHub = connection;

        // Act
        await Drain(gameHub.StreamAsync<string>("SendFlee", TestContext.Current.CancellationToken));

        // Assert
        var updated = await combatUpdatedReceived.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(enemy.Name, Assert.Single(updated.Combatants, c => !c.IsPlayer).Name);
        Assert.Equal(TRPG.Combat.Responses.CombatOutcome.Fled, updated.Outcome);
    }

    [Fact]
    public async Task ResolveCombatAction_PublishesCombatUpdatedAndSceneSnapshot_WhenTheAttackChangesNearbyCreatureState()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();
        await StartFight(sessionId, enemy);
        var connection = fixture.CreateHubConnection(sessionId);
        var combatUpdatedReceived =
            new TaskCompletionSource<TRPG.Combat.Responses.CombatUpdatePayload>();
        var initialSnapshotReceived =
            new TaskCompletionSource<TRPG.GameSessions.Responses.SceneSnapshot>();
        var sceneSnapshots = new List<TRPG.GameSessions.Responses.SceneSnapshot>();
        var gameClient = new TestGameClient
        {
            OnCombatUpdated = payload => combatUpdatedReceived.TrySetResult(payload),
            OnSceneSnapshot = snapshot =>
            {
                sceneSnapshots.Add(snapshot);
                initialSnapshotReceived.TrySetResult(snapshot);
            },
        };
        connection.Register<IGameClient>(gameClient);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await initialSnapshotReceived.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        sceneSnapshots.Clear();
        await using var gameHub = connection;

        // Act
        await gameHub.InvokeAsync(
            "ResolveUseAbilityCombatAction",
            enemy.Id,
            "Strike",
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await combatUpdatedReceived.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(enemy.Name, Assert.Single(updated.Combatants, c => !c.IsPlayer).Name);
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var freshEnemy = await context.Creatures.SingleAsync(
            c => c.Id == enemy.Id,
            TestContext.Current.CancellationToken
        );
        var scene = Assert.Single(sceneSnapshots);
        var updatedEnemy = Assert.Single(scene.NearbyCreatures, c => c.Id == enemy.Id);
        Assert.Equal(freshEnemy.CurrentHp, updatedEnemy.CurrentHp);
    }

    [Fact]
    public async Task SendChat_PublishesExactlyOneSceneSnapshot_WhenMovingTriggersCatchUp()
    {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var destinationDistrictId = Guid.NewGuid();
        var destinationLocation = Builders.MakeLocation(
            _worldId,
            _stateId,
            districtId: destinationDistrictId
        );
        var destinationDistrict = Builders.MakeDistrict(
            _cityId,
            DataDistrictType.Residential,
            worldId: _worldId,
            name: "Market Row",
            id: destinationDistrictId,
            locationId: destinationLocation.Id
        );
        var connector = Builders.MakeLocationConnector(
            _locationId,
            destinationLocationId: destinationDistrict.LocationId,
            worldId: _worldId,
            name: "Path",
            description: "A path leading to Market Row.",
            destinationLabel: destinationDistrict.Name
        );
        context.Districts.Add(destinationDistrict);
        context.Locations.Add(destinationLocation);
        context.LocationConnectors.Add(connector);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sessionId = await StartSession();
        var connection = fixture.CreateHubConnection(sessionId);
        var sceneSnapshots = new List<TRPG.GameSessions.Responses.SceneSnapshot>();
        var initialSnapshotReceived =
            new TaskCompletionSource<TRPG.GameSessions.Responses.SceneSnapshot>();
        var gameClient = new TestGameClient
        {
            OnSceneSnapshot = snapshot =>
            {
                sceneSnapshots.Add(snapshot);
                initialSnapshotReceived.TrySetResult(snapshot);
            },
        };
        connection.Register<IGameClient>(gameClient);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await initialSnapshotReceived.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        sceneSnapshots.Clear();
        await using var gameHub = connection;

        fixture.ChatClient.PendingToolCallName = "move";
        fixture.ChatClient.PendingToolCallArguments = new Dictionary<string, object?>
        {
            ["destinationName"] = destinationDistrict.Name,
        };

        // Act
        await Drain(
            gameHub.StreamAsync<string>(
                "SendChat",
                $"I head to {destinationDistrict.Name}",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Single(sceneSnapshots);
    }

    [Fact]
    public async Task ResolveCombatAction_ThrowsHubException_WhenNoFightIsActive()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(() =>
            gameHub.InvokeAsync(
                "ResolveUseAbilityCombatAction",
                Guid.NewGuid(),
                "Strike",
                TestContext.Current.CancellationToken
            )
        );
        Assert.Contains(
            "There's no fight to act in right now.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task ResolveCombatAction_ThrowsHubException_WhenActionIsInvalid()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();
        await StartFight(sessionId, enemy);
        await using var gameHub = await Connect(sessionId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(() =>
            gameHub.InvokeAsync(
                "ResolveUseAbilityCombatAction",
                enemy.Id,
                "Nonexistent Move",
                TestContext.Current.CancellationToken
            )
        );
        Assert.Contains(
            "Ability Nonexistent Move not found",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task SendFlee_ReturnsAMessage_WhenNoFightIsActive()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var narration = await Drain(
            gameHub.StreamAsync<string>("SendFlee", TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal("There's no fight to flee from right now.", narration);
    }

    [Fact]
    public async Task EndSession_KeepsPlaytimeAtZero_WhenNoMessagesWereSent()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        await gameHub.InvokeAsync("EndSession", TestContext.Current.CancellationToken);

        // Assert — in-game time only advances from messages/waits, never from real time alone
        var world = await GetWorld();
        Assert.Equal(TimeSpan.Zero, world.Playtime);
    }

    [Fact]
    public async Task EndSession_SavesAdvancedPlaytime_AfterChatting()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);
        await Drain(
            gameHub.StreamAsync<string>(
                "SendChat",
                "I look around.",
                TestContext.Current.CancellationToken
            )
        );

        // Act
        await gameHub.InvokeAsync("EndSession", TestContext.Current.CancellationToken);

        // Assert
        var world = await GetWorld();
        Assert.True(world.Playtime > TimeSpan.Zero);
    }

    [Fact]
    public async Task StartTheftEncounterNarration_NarratesAndPublishesTheEncounter()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var connection = fixture.CreateHubConnection(sessionId);
        var initialSceneReceived = new TaskCompletionSource<SceneSnapshot>();
        var encounterStarted =
            new TaskCompletionSource<TRPG.Encounters.Responses.TheftEncounterState>();
        connection.Register<IGameClient>(
            new TestGameClient
            {
                OnSceneSnapshot = snapshot => initialSceneReceived.TrySetResult(snapshot),
                OnTheftEncounterStarted = state => encounterStarted.TrySetResult(state),
            }
        );
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await initialSceneReceived.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        var (encounter, owner) = await SeedActiveTheftEncounter();

        // Act
        var narration = await Drain(
            connection.StreamAsync<string>(
                "StartTheftEncounterNarration",
                encounter.Id,
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal(fixture.ChatClient.ChatResponseText, narration);
        var state = await encounterStarted.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(encounter.Id, state.EncounterId);
        Assert.Equal(owner.Name, state.OwnerName);
        Assert.Equal(["Silver Ring"], state.ItemNames);
        Assert.Equal(["Apologize", "Fight"], state.AllowedActions);
    }

    [Fact]
    public async Task Reconnect_PushesTheftEncounterStarted_WhenPlayerHasAnActiveTheftEncounter()
    {
        // Arrange
        var (encounter, owner) = await SeedActiveTheftEncounter();
        var sessionId = await StartSession();
        var encounterStarted =
            new TaskCompletionSource<TRPG.Encounters.Responses.TheftEncounterState>();
        await using var connection = fixture.CreateHubConnection(sessionId);
        connection.Register<IGameClient>(
            new TestGameClient
            {
                OnTheftEncounterStarted = state => encounterStarted.TrySetResult(state),
            }
        );

        // Act
        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var state = await encounterStarted.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(encounter.Id, state.EncounterId);
        Assert.Equal(owner.Name, state.OwnerName);
        Assert.Equal(["Silver Ring"], state.ItemNames);
        Assert.Equal(["Apologize", "Fight"], state.AllowedActions);
    }

    [Fact]
    public async Task Reconnect_PushesEncounterStarted_WhenPlayerHasAnActiveEncounter()
    {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var faction = Builders.MakeFaction(_worldId, aggression: 150);
        var monster = Builders.MakeCreature(
            _worldId,
            name: "Ravenous Wolf",
            creatureType: DataCreatureType.Beast,
            locationId: _locationId,
            level: 1
        );
        var encounter = Builders.MakeHostileEncounter(
            _worldId,
            _playerId,
            _locationId,
            factionName: faction.Name,
            members:
            [
                new HostileEncounterMemberSnapshot(
                    monster.Id,
                    monster.Name,
                    monster.CreatureType,
                    monster.Level
                ),
            ]
        );
        context.Factions.Add(faction);
        context.Creatures.Add(monster);
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sessionId = await StartSession();
        var encounterStartedReceived =
            new TaskCompletionSource<TRPG.Encounters.Responses.HostileEncounterState>();
        await using var connection = fixture.CreateHubConnection(sessionId);
        var gameClient = new TestGameClient
        {
            OnHostileEncounterStarted = state => encounterStartedReceived.TrySetResult(state),
        };
        connection.Register<IGameClient>(gameClient);

        // Act
        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        var state = await encounterStartedReceived.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(faction.Name, state.FactionName);
        Assert.Equal(monster.Name, Assert.Single(state.Members).Name);
    }
}
