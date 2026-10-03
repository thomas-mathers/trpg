using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Serialization;
using TRPG.Application.Configuration;
using TRPG.Application.WorldGeneration;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.GameSessions.Hubs;
using TRPG.GameSessions.Responses;
using TRPG.Tests.Helpers;
using TypedSignalR.Client;
using DataBuildingType = TRPG.Domain.Models.BuildingType;
using DataCreatureCondition = TRPG.Domain.Models.CreatureCondition;
using DataCreaturePosture = TRPG.Domain.Models.CreaturePosture;
using DataCreatureType = TRPG.Domain.Models.CreatureType;
using DataDistrictType = TRPG.Domain.Models.DistrictType;
using ResponseCreaturePosture = TRPG.GameSessions.Responses.CreaturePosture;

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
        fixture.ChatClient.TextBeforeToolCall = null;
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

    private async Task SetPlayerPosture(DataCreaturePosture posture)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        await context
            .Creatures.Where(creature => creature.Id == _playerId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.Posture, posture),
                TestContext.Current.CancellationToken
            );
    }

    private async Task<HubConnection> Connect(Guid sessionId)
    {
        var connection = fixture.CreateHubConnection(sessionId);
        connection.Register<IGameClient>(new TestGameClient { Connection = connection });
        await connection.StartAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private async Task<ConnectedClient> ConnectAndAwaitInitialSnapshot(
        Guid sessionId,
        List<SceneSnapshot> sceneSnapshots
    )
    {
        var connection = fixture.CreateHubConnection(sessionId);
        var initialSnapshotReceived = new TaskCompletionSource<SceneSnapshot>();
        var gameClient = new TestGameClient
        {
            Connection = connection,
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
        return new ConnectedClient(connection, gameClient);
    }

    private sealed record ConnectedClient(HubConnection Connection, TestGameClient Client);

    private static Task<SceneSnapshot> CaptureNextSnapshot(
        ConnectedClient connected,
        List<SceneSnapshot> snapshots
    )
    {
        var received = new TaskCompletionSource<SceneSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        connected.Client.OnSceneSnapshot = snapshot =>
        {
            snapshots.Add(snapshot);
            received.TrySetResult(snapshot);
        };
        return received.Task;
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

    private static Task<ActionResult> Act(HubConnection hub, string method, params object[] args) =>
        hub.InvokeCoreAsync<ActionResult>(method, args, TestContext.Current.CancellationToken);

    private static async Task<string> Drain(IAsyncEnumerable<string> tokens)
    {
        var builder = new StringBuilder();
        await foreach (var token in tokens)
        {
            builder.Append(token);
        }
        return builder.ToString();
    }

    private async Task<TRPG.Domain.Models.ChatMessage> GetChatMessage(Guid sessionId, string role)
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

    private static void AssertOnlyClockDriftElapsed(World world)
    {
        Assert.True(world.GameTime >= GameClock.Epoch);
        Assert.True(world.GameTime < GameClock.Epoch + TimeSpan.FromMinutes(1));
    }

    private async Task CheckpointRunningClock()
    {
        await Task.Delay(1100, TestContext.Current.CancellationToken);
        await using var scope = fixture.CreateScope();
        var worldClock = scope.ServiceProvider.GetRequiredService<IWorldClock>();
        await worldClock.Checkpoint(_worldId, TestContext.Current.CancellationToken);
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
            Items = [new TheftCrimeItem(Guid.NewGuid(), "Silver Ring", 1)],
        };
        var encounter = new TheftEncounter
        {
            WorldId = _worldId,
            PlayerId = _playerId,
            LocationId = _locationId,
            TheftCrimeId = crime.Id,
            ConfrontingCreatureId = owner.Id,
            ConfrontingName = owner.Name,
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

    private async Task<Guid> SeedTempleInExistingCity()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var cityEntranceDistrict = Builders.MakeDistrict(
            _cityId,
            DataDistrictType.CityEntrance,
            worldId: _worldId
        );
        var cityEntranceLocation = Builders.MakeLocation(
            _worldId,
            _stateId,
            cityId: _cityId,
            districtId: cityEntranceDistrict.Id,
            id: cityEntranceDistrict.LocationId
        );
        var holySiteDistrict = Builders.MakeDistrict(
            _cityId,
            DataDistrictType.HolySite,
            worldId: _worldId
        );
        var templeExteriorLocation = Builders.MakeLocation(
            _worldId,
            _stateId,
            cityId: _cityId,
            districtId: holySiteDistrict.Id,
            id: holySiteDistrict.LocationId
        );
        var temple = Builders.MakeBuilding(
            exteriorLocationId: templeExteriorLocation.Id,
            worldId: _worldId,
            buildingType: DataBuildingType.Temple
        );
        var sanctuaryLocation = Builders.MakeLocation(
            _worldId,
            _stateId,
            coarseAnchorLocationId: cityEntranceLocation.Id
        );
        var sanctuaryRoom = Builders.MakeRoom(
            temple.Id,
            worldId: _worldId,
            locationId: sanctuaryLocation.Id,
            name: TempleRoomNames.Sanctuary
        );

        context.Districts.AddRange(cityEntranceDistrict, holySiteDistrict);
        context.Locations.AddRange(cityEntranceLocation, templeExteriorLocation, sanctuaryLocation);
        context.Buildings.Add(temple);
        context.Rooms.Add(sanctuaryRoom);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await context
            .Locations.Where(location => location.Id == _locationId)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(
                        location => location.CoarseAnchorLocationId,
                        cityEntranceLocation.Id
                    ),
                TestContext.Current.CancellationToken
            );

        return sanctuaryLocation.Id;
    }

    private async Task KillPlayer()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var player = await context.Creatures.SingleAsync(
            c => c.Id == _playerId,
            TestContext.Current.CancellationToken
        );
        player.Die();
        player.CurrentHp = 0;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
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
            Connection = connection,
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
        Assert.True(snapshot.Version > 0);
        Assert.True(snapshot.AnchoredAtUnixMilliseconds > 0);
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
            Connection = firstConnection,
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
            Connection = secondConnection,
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
        Assert.True(secondSnapshot.Version > firstSnapshot.Version);
    }

    [Fact]
    public async Task SendWait_AdvancesTime()
    {
        // Arrange
        await SetPlayerPosture(DataCreaturePosture.Sitting);
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var result = await Act(gameHub, "SendWait", 3, 0);

        // Assert
        Assert.True(result.Succeeded);
        var world = await GetWorld();
        Assert.True(world.GameTime > GameClock.Epoch);
    }

    [Fact]
    public async Task SendWait_AdvancesTime_WhenOnlyMinutesAreProvided()
    {
        // Arrange
        await SetPlayerPosture(DataCreaturePosture.Sitting);
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var result = await Act(gameHub, "SendWait", 0, 30);

        // Assert
        Assert.True(result.Succeeded);
        var world = await GetWorld();
        Assert.True(world.GameTime > GameClock.Epoch);
    }

    [Fact]
    public async Task SendWait_ReturnsAMessage_WhenDurationIsNotPositive()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);
        await CheckpointRunningClock();

        // Act
        var result = await Act(gameHub, "SendWait", 0, 0);

        // Assert
        Assert.Equal(ActionFailureReason.InvalidDuration, result.Reason);
        var world = await GetWorld();
        AssertOnlyClockDriftElapsed(world);
    }

    [Fact]
    public async Task SendActivateTrigger_LeavesTheTriggerUnactivated_WhenItsQuestIsNotAccepted()
    {
        var quest = Builders.MakeQuest(Guid.NewGuid(), worldId: _worldId);
        var trigger = Builders.MakeTrigger(_worldId, _locationId);
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            context.Quests.Add(quest);
            context.Props.Add(trigger);
            context.QuestObjectives.Add(
                Builders.MakeInteractWithPropObjective(quest.Id, trigger.Id, worldId: _worldId)
            );
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        var result = await Act(gameHub, "SendActivateTrigger", trigger.Id);

        Assert.Equal(ActionFailureReason.NothingToActivate, result.Reason);
        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var stored = await verifyContext
            .Props.OfType<Trigger>()
            .SingleAsync(t => t.Id == trigger.Id, TestContext.Current.CancellationToken);
        Assert.False(stored.IsActivated);
    }

    [Fact]
    public async Task SendWait_DoesNotAdvanceTime_WhenPlayerIsNotSitting()
    {
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        var result = await Act(gameHub, "SendWait", 1, 0);

        Assert.Equal(ActionFailureReason.NotSitting, result.Reason);
        var world = await GetWorld();
        AssertOnlyClockDriftElapsed(world);
    }

    [Fact]
    public async Task SendWait_DoesNotAdvanceTime_WhenDurationExceedsTwentyFourHours()
    {
        await SetPlayerPosture(DataCreaturePosture.Sitting);
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        var result = await Act(gameHub, "SendWait", 24, 1);

        Assert.Equal(ActionFailureReason.InvalidDuration, result.Reason);
        var world = await GetWorld();
        AssertOnlyClockDriftElapsed(world);
    }

    [Fact]
    public async Task SendSitDownAndStandUp_PublishSeatAndPlayerState()
    {
        var seat = Builders.MakeSeat(_worldId, _locationId);
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            context.Props.Add(seat);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var sessionId = await StartSession();
        var snapshots = new List<SceneSnapshot>();
        var connected = await ConnectAndAwaitInitialSnapshot(sessionId, snapshots);
        await using var gameHub = connected.Connection;
        var seatedReceived = CaptureNextSnapshot(connected, snapshots);

        var sitResult = await Act(gameHub, "SendSitDown", seat.Id);

        Assert.True(sitResult.Succeeded);
        await seatedReceived.WaitAsync(PushTimeout, TestContext.Current.CancellationToken);
        var seated = Assert.Single(snapshots);
        Assert.Equal(ResponseCreaturePosture.Sitting, seated.PlayerStatus.Posture);
        var seatedProp = Assert.Single(seated.NearbyProps, prop => prop.Id == seat.Id);
        Assert.True(seatedProp.IsOccupied);
        Assert.True(seatedProp.IsOccupiedByPlayer);

        snapshots.Clear();
        var standingReceived = CaptureNextSnapshot(connected, snapshots);
        var standResult = await Act(gameHub, "SendStandUp");

        Assert.True(standResult.Succeeded);
        await standingReceived.WaitAsync(PushTimeout, TestContext.Current.CancellationToken);
        var standing = Assert.Single(snapshots);
        Assert.Equal(ResponseCreaturePosture.Standing, standing.PlayerStatus.Posture);
        var standingProp = Assert.Single(standing.NearbyProps, prop => prop.Id == seat.Id);
        Assert.False(standingProp.IsOccupied);
        Assert.False(standingProp.IsOccupiedByPlayer);
        Assert.True(standing.Version > seated.Version);
    }

    [Fact]
    public async Task SendSleep_AdvancesTime_WhenTheBedIsRentedToThePlayer()
    {
        // Arrange
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            context.Props.Add(
                Builders.MakeBed(_worldId, locationId: _locationId, assignedCreatureId: _playerId)
            );
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var result = await Act(gameHub, "SendSleep", 8, 0);

        // Assert
        Assert.True(result.Succeeded);
        var world = await GetWorld();
        Assert.True(world.GameTime > GameClock.Epoch);
    }

    [Fact]
    public async Task SendSleep_DoesNotAdvanceTime_WhenDurationExceedsTwentyFourHours()
    {
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        var result = await Act(gameHub, "SendSleep", 25, 0);

        Assert.Equal(ActionFailureReason.InvalidDuration, result.Reason);
        var world = await GetWorld();
        AssertOnlyClockDriftElapsed(world);
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
            Connection = connection,
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
    public async Task SendFlee_EndsTheFight_AndSucceeds()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();
        await StartFight(sessionId, enemy);
        await using var gameHub = await Connect(sessionId);

        // Act
        var result = await Act(gameHub, "SendFlee");

        // Assert
        Assert.True(result.Succeeded);
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
        var combatUpdatedReceived = new TaskCompletionSource<TRPG.Combat.Responses.CombatUpdated>();
        var gameClient = new TestGameClient
        {
            Connection = connection,
            OnCombatUpdated = payload => combatUpdatedReceived.TrySetResult(payload),
        };
        connection.Register<IGameClient>(gameClient);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await using var gameHub = connection;

        // Act
        await Act(gameHub, "SendFlee");

        // Assert
        var updated = await combatUpdatedReceived.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(enemy.Name, Assert.Single(updated.Combatants, c => !c.IsPlayer).Name);
        Assert.Equal(TRPG.Combat.Responses.CombatOutcome.Fled, updated.Outcome);
    }

    [Fact]
    public async Task SendFlee_Succeeds_WhenTheFleeAttemptFails()
    {
        // Arrange — EndpointTestFixture pins Flee:MinimumCatchChance/MaximumCatchChance to 0
        // (guaranteed success) for every other test, so this one spins up its own factory
        // against the same already-migrated database with the opposite bounds (guaranteed
        // catch) to exercise the failure path.
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();

        var chatClient = new FakeChatClient();
        await using var factory = TestWebApplicationFactory.Create(
            fixture.ConnectionString,
            chatClient,
            new Dictionary<string, string?>
            {
                ["Flee:MinimumCatchChance"] = "1",
                ["Flee:MaximumCatchChance"] = "1",
            }
        );

        chatClient.PendingToolCallName = "attack";
        chatClient.PendingToolCallArguments = new Dictionary<string, object?>
        {
            ["abilityName"] = "Strike",
            ["targetName"] = enemy.Name,
        };
        await using (var setupHub = ConnectHub(factory, sessionId))
        {
            await setupHub.StartAsync(TestContext.Current.CancellationToken);
            await Drain(
                setupHub.StreamAsync<string>(
                    "SendChat",
                    $"I attack {enemy.Name}",
                    TestContext.Current.CancellationToken
                )
            );
        }

        await using var gameHub = ConnectHub(factory, sessionId);
        await gameHub.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await Act(gameHub, "SendFlee");

        // Assert
        Assert.True(result.Succeeded);
        return;

        static HubConnection ConnectHub(WebApplicationFactory<Program> factory, Guid sessionId)
        {
            var uri = new Uri(factory.Server.BaseAddress, $"/hubs/chat?sessionId={sessionId}");
            var connection = new HubConnectionBuilder()
                .WithUrl(
                    uri,
                    options =>
                    {
                        options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                        options.Transports = HttpTransportType.LongPolling;
                    }
                )
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.PropertyNamingPolicy = TrpgJsonOptions
                        .Default
                        .PropertyNamingPolicy;
                    options.PayloadSerializerOptions.PropertyNameCaseInsensitive = TrpgJsonOptions
                        .Default
                        .PropertyNameCaseInsensitive;
                    options.PayloadSerializerOptions.DefaultIgnoreCondition = TrpgJsonOptions
                        .Default
                        .DefaultIgnoreCondition;
                    foreach (var converter in TrpgJsonOptions.Default.Converters)
                    {
                        options.PayloadSerializerOptions.Converters.Add(converter);
                    }
                })
                .Build();
            connection.Register<IGameClient>(new TestGameClient { Connection = connection });
            return connection;
        }
    }

    [Fact]
    public async Task ResolveCombatAction_PublishesCombatUpdatedAndSceneSnapshot_WhenTheAttackChangesNearbyCreatureState()
    {
        // Arrange
        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();
        await StartFight(sessionId, enemy);
        var connection = fixture.CreateHubConnection(sessionId);
        var combatUpdatedReceived = new TaskCompletionSource<TRPG.Combat.Responses.CombatUpdated>();
        var snapshotReceived =
            new TaskCompletionSource<TRPG.GameSessions.Responses.SceneSnapshot>();
        var sceneSnapshots = new List<TRPG.GameSessions.Responses.SceneSnapshot>();
        var gameClient = new TestGameClient
        {
            Connection = connection,
            OnCombatUpdated = payload => combatUpdatedReceived.TrySetResult(payload),
            OnSceneSnapshot = snapshot =>
            {
                sceneSnapshots.Add(snapshot);
                snapshotReceived.TrySetResult(snapshot);
            },
        };
        connection.Register<IGameClient>(gameClient);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await snapshotReceived.Task.WaitAsync(PushTimeout, TestContext.Current.CancellationToken);
        sceneSnapshots.Clear();
        // CombatUpdated and SceneSnapshot are pushed as two separate, sequentially-dispatched
        // messages after the combat action resolves; awaiting only the first doesn't guarantee
        // the second's client-side callback has already run, so this is reset to wait for it too.
        snapshotReceived = new TaskCompletionSource<TRPG.GameSessions.Responses.SceneSnapshot>();
        await using var gameHub = connection;

        // Act
        await Act(gameHub, "ResolveUseAbilityCombatAction", enemy.Id, "Strike");

        // Assert
        var updated = await combatUpdatedReceived.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(enemy.Name, Assert.Single(updated.Combatants, c => !c.IsPlayer).Name);
        await snapshotReceived.Task.WaitAsync(PushTimeout, TestContext.Current.CancellationToken);
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
    public async Task SendMove_PublishesExactlyOneSceneSnapshot_WhenMovingTriggersCatchUp()
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
        var sceneSnapshots = new List<TRPG.GameSessions.Responses.SceneSnapshot>();
        var connected = await ConnectAndAwaitInitialSnapshot(sessionId, sceneSnapshots);
        await using var gameHub = connected.Connection;
        var snapshotReceived = CaptureNextSnapshot(connected, sceneSnapshots);

        // Act
        await Act(gameHub, "SendMove", connector.Id);

        // Assert
        await snapshotReceived.WaitAsync(PushTimeout, TestContext.Current.CancellationToken);
        Assert.Single(sceneSnapshots);
    }

    [Fact]
    public async Task SendMove_DeliversDepartureEncounter_WhenMoveIsIntercepted()
    {
        var sessionId = await StartSession();
        await using var connection = fixture.CreateHubConnection(sessionId);
        var ready = new TaskCompletionSource();
        var order = new ConcurrentQueue<string>();
        connection.Register<IGameClient>(
            new TestGameClient
            {
                Connection = connection,
                OnSceneSnapshot = _ => ready.TrySetResult(),
                OnHostileEncounterStarted = _ => order.Enqueue("encounter"),
                OnHostileEncounterResolved = _ => order.Enqueue("resolved"),
            }
        );
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await ready.Task.WaitAsync(PushTimeout, TestContext.Current.CancellationToken);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var destination = Builders.MakeLocation(_worldId, _stateId);
        var faction = Builders.MakeFaction(_worldId, aggression: 150);
        var monster = Builders.MakeCreature(_worldId, locationId: _locationId);
        var group = Builders.MakeEncounterGroup(_worldId, _locationId, faction.Id);
        var connector = Builders.MakeLocationConnector(
            _locationId,
            destinationLocationId: destination.Id,
            worldId: _worldId,
            destinationLabel: "Elsewhere"
        );
        context.Locations.Add(destination);
        context.LocationConnectors.Add(connector);
        context.Factions.Add(faction);
        context.Creatures.Add(monster);
        context.EncounterGroups.Add(group);
        context.EncounterGroupMembers.Add(
            Builders.MakeEncounterGroupMember(_worldId, group.Id, monster.Id)
        );
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Act(connection, "SendMove", connector.Id);

        Assert.Single(order);
        var player = await context
            .Creatures.AsNoTracking()
            .SingleAsync(
                creature => creature.Id == _playerId,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(_locationId, player.LocationId);
        var arrivalEnemy = Builders.MakeCreature(_worldId, locationId: destination.Id);
        var arrivalGroup = Builders.MakeEncounterGroup(_worldId, destination.Id, faction.Id);
        context.Creatures.Add(arrivalEnemy);
        context.EncounterGroups.Add(arrivalGroup);
        context.EncounterGroupMembers.Add(
            Builders.MakeEncounterGroupMember(_worldId, arrivalGroup.Id, arrivalEnemy.Id)
        );
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Act(connection, "ResolveFleeEncounterAction");

        var movedPlayer = await context
            .Creatures.AsNoTracking()
            .SingleAsync(
                creature => creature.Id == _playerId,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(destination.Id, movedPlayer.LocationId);
        Assert.Equal(["encounter", "resolved", "encounter"], order.ToArray());
    }

    [Fact]
    public async Task ResolveCombatAction_ThrowsHubException_WhenNoFightIsActive()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(() =>
            Act(gameHub, "ResolveUseAbilityCombatAction", Guid.NewGuid(), "Strike")
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
            Act(gameHub, "ResolveUseAbilityCombatAction", enemy.Id, "Nonexistent Move")
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
        var result = await Act(gameHub, "SendFlee");

        // Assert
        Assert.Equal(ActionFailureReason.NoFight, result.Reason);
    }

    [Fact]
    public async Task SendFlee_PublishesSceneSnapshot_WhenFleeingRelocatesThePlayer()
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

        var enemy = await SeedHostileCreature();
        var sessionId = await StartSession();
        await StartFight(sessionId, enemy);
        var sceneSnapshots = new List<SceneSnapshot>();
        var connected = await ConnectAndAwaitInitialSnapshot(sessionId, sceneSnapshots);
        await using var gameHub = connected.Connection;
        var snapshotReceived = CaptureNextSnapshot(connected, sceneSnapshots);

        // Act
        await Act(gameHub, "SendFlee");

        // Assert
        await snapshotReceived.WaitAsync(PushTimeout, TestContext.Current.CancellationToken);
        var scene = Assert.Single(sceneSnapshots);
        Assert.Equal(destinationDistrict.Name, scene.DistrictName);
    }

    [Fact]
    public async Task SendRespawn_RelocatesPlayerAndDropsCorpse()
    {
        // Arrange - the corpse must keep at least one item, or MovePlayerCommand cleans up the
        // now-empty corpse as part of relocating the player away from the death location.
        var sanctuaryLocationId = await SeedTempleInExistingCity();
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            var item = Builders.MakeWeapon(_worldId, quantity: 1);
            item.Ownership.OwnerId = _playerId;
            item.Ownership.OwnerType = OwnerType.Creature;
            context.Items.Add(item);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await KillPlayer();
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var result = await Act(gameHub, "SendRespawn");

        // Assert
        Assert.True(result.Succeeded);
        await using var scope2 = fixture.CreateScope();
        var context2 = scope2.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var player = await context2.Creatures.SingleAsync(
            c => c.Id == _playerId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(sanctuaryLocationId, player.LocationId);
        Assert.Equal(DataCreatureCondition.Awake, player.Condition);
        var corpse = await context2.Creatures.SingleAsync(
            c => c.PlayerCorpseOwnerId == _playerId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_locationId, corpse.LocationId);
    }

    [Fact]
    public async Task SendRespawn_ReturnsAMessage_WhenPlayerIsNotDead()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var result = await Act(gameHub, "SendRespawn");

        // Assert
        Assert.Equal(ActionFailureReason.NotDead, result.Reason);
    }

    [Fact]
    public async Task EndSession_CheckpointsElapsedConnectedTime_WhenNoMessagesWereSent()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        await gameHub.InvokeAsync("EndSession", TestContext.Current.CancellationToken);

        // Assert
        var world = await GetWorld();
        Assert.True(world.GameTime > GameClock.Epoch);
    }

    [Fact]
    public async Task EndSession_DoesNotAddFixedGameTime_ForChatMessages()
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
        Assert.True(world.GameTime > GameClock.Epoch);
        Assert.True(world.GameTime < GameClock.Epoch + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task StartTheftEncounter_PublishesTheEncounter()
    {
        // Arrange
        var sessionId = await StartSession();
        var encounterStarted =
            new TaskCompletionSource<TRPG.Encounters.Responses.TheftEncounterState>();
        var connected = await ConnectAndAwaitInitialSnapshot(sessionId, []);
        await using var connection = connected.Connection;
        connected.Client.OnTheftEncounterStarted = state => encounterStarted.TrySetResult(state);
        var (encounter, owner) = await SeedActiveTheftEncounter();

        // Act
        var result = await Act(connection, "StartTheftEncounter", encounter.Id);

        // Assert
        Assert.True(result.Succeeded);
        var state = await encounterStarted.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(encounter.Id, state.EncounterId);
        Assert.Equal(owner.Name, state.ConfrontingName);
        Assert.Equal(["Silver Ring"], state.ItemNames);
        Assert.Equal(["Apologize", "Flee"], state.AllowedActions);
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
                Connection = connection,
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
        Assert.Equal(owner.Name, state.ConfrontingName);
        Assert.Equal(["Silver Ring"], state.ItemNames);
        Assert.Equal(["Apologize", "Flee"], state.AllowedActions);
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
            Connection = connection,
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

    [Fact]
    public async Task ResolveFleeEncounterAction_PublishesSceneSnapshot_WhenEncounterResolves()
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
        var previousLocation = Builders.MakeLocation(_worldId, _stateId);
        context.Factions.Add(faction);
        context.Creatures.Add(monster);
        context.Encounters.Add(encounter);
        context.Locations.Add(previousLocation);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // No exit connector is seeded at this location, so a successful flee falls back to the
        // player's previous location.
        var trackedPlayer = await context.Creatures.SingleAsync(
            c => c.Id == _playerId,
            TestContext.Current.CancellationToken
        );
        trackedPlayer.PreviousLocationId = previousLocation.Id;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sessionId = await StartSession();
        var sceneSnapshots = new List<SceneSnapshot>();
        var connected = await ConnectAndAwaitInitialSnapshot(sessionId, sceneSnapshots);
        await using var gameHub = connected.Connection;
        var snapshotReceived = CaptureNextSnapshot(connected, sceneSnapshots);

        // Act
        await Act(gameHub, "ResolveFleeEncounterAction");

        // Assert
        await snapshotReceived.WaitAsync(PushTimeout, TestContext.Current.CancellationToken);
        Assert.NotEmpty(sceneSnapshots);
    }

    [Fact]
    public async Task ResolvePayFineEncounterAction_PublishesSceneSnapshot_WhenEncounterResolves()
    {
        // Arrange
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var cityFaction = Builders.MakeFaction(_worldId, isCityFaction: true);
        var guard = Builders.MakeCreature(
            _worldId,
            profession: TRPG.Domain.Models.Profession.Guard,
            locationId: _locationId
        );
        var encounter = Builders.MakeGuardEncounter(
            _worldId,
            _playerId,
            _locationId,
            guard.Id,
            cityFaction.Id,
            fineAmount: 50
        );
        var gold = Builders.MakeGold(_worldId, quantity: 100);
        gold.Ownership.OwnerId = _playerId;
        gold.Ownership.OwnerType = OwnerType.Creature;
        context.Factions.Add(cityFaction);
        context.Creatures.Add(guard);
        context.Encounters.Add(encounter);
        context.Items.Add(gold);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sessionId = await StartSession();
        var sceneSnapshots = new List<SceneSnapshot>();
        var connected = await ConnectAndAwaitInitialSnapshot(sessionId, sceneSnapshots);
        await using var gameHub = connected.Connection;
        var snapshotReceived = CaptureNextSnapshot(connected, sceneSnapshots);

        // Act
        await Act(gameHub, "ResolvePayFineEncounterAction");

        // Assert - paying the fine deducts gold, which the scene diff must catch
        await snapshotReceived.WaitAsync(PushTimeout, TestContext.Current.CancellationToken);
        var scene = Assert.Single(sceneSnapshots);
        Assert.Equal(50, scene.PlayerStatus.Gold);

        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var updatedEncounter = await verifyContext.Encounters.SingleAsync(
            e => e.Id == encounter.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(EncounterState.Completed, updatedEncounter.State);
    }
}
