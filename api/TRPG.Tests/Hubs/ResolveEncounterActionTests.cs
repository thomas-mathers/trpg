using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Encounters.Responses;
using TRPG.GameSessions.Hubs;
using TRPG.GameSessions.Responses;
using TRPG.Tests.Helpers;
using TypedSignalR.Client;
using DataCreatureType = TRPG.Domain.Models.CreatureType;

namespace TRPG.Tests.Hubs;

[Collection("Endpoints")]
public sealed class ResolveEncounterActionTests(EndpointTestFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan PushTimeout = TimeSpan.FromSeconds(10);

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

    [Fact]
    public async Task ResolveEncounterAction_ReturnsAMessage_WhenNoEncounterIsActive()
    {
        // Arrange
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var narration = await Drain(
            gameHub.StreamAsync<string>(
                "ResolveAttackEncounterAction",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal("There's no encounter to resolve right now.", narration);
    }

    private async Task<(Faction Faction, Creature Monster)> SeedHostileGroup()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var faction = Builders.MakeFaction(_worldId, aggression: 150);
        var monster = Builders.MakeCreature(
            _worldId,
            creatureType: DataCreatureType.Beast,
            locationId: _locationId,
            level: 1
        );
        context.Factions.Add(faction);
        context.Creatures.Add(monster);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (faction, monster);
    }

    private async Task<HostileEncounter> SeedActiveEncounter(
        Faction faction,
        Creature monster,
        Guid? arrivalOriginLocationId = null
    )
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

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
            ],
            arrivalOriginLocationId: arrivalOriginLocationId
        );
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return encounter;
    }

    private async Task<TRPG.Domain.Models.Encounter> GetEncounter(Guid encounterId)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        return await context.Encounters.SingleAsync(
            e => e.Id == encounterId,
            TestContext.Current.CancellationToken
        );
    }

    private async Task<FightEncounter?> FindFight(Guid playerId)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        return await context
            .Encounters.OfType<FightEncounter>()
            .SingleOrDefaultAsync(
                f => f.PlayerId == playerId,
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

    [Fact]
    public async Task ResolveEncounterAction_Attack_CompletesTheEncounter_AndStartsAFight_WithOnlyTheGroupsMembers()
    {
        // Arrange
        var (faction, monster) = await SeedHostileGroup();
        var encounter = await SeedActiveEncounter(faction, monster);
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var narration = await Drain(
            gameHub.StreamAsync<string>(
                "ResolveAttackEncounterAction",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal(fixture.ChatClient.ChatResponseText, narration);

        var persistedEncounter = await GetEncounter(encounter.Id);
        Assert.Equal(EncounterState.Completed, persistedEncounter.State);
        Assert.NotNull(persistedEncounter.CompletedAt);

        var fight = await FindFight(_playerId);
        Assert.NotNull(fight);
        Assert.Equal(
            new[] { _playerId, monster.Id }.OrderBy(id => id),
            fight.CombatantIds.OrderBy(id => id)
        );
    }

    [Fact]
    public async Task ResolveEncounterAction_Evade_AlwaysCompletesTheEncounter_AndConsistentlyLinksAnyFightThatStarts()
    {
        // Arrange
        var (faction, monster) = await SeedHostileGroup();
        var encounter = await SeedActiveEncounter(faction, monster);
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act — the evade roll is random, so this asserts the invariants that hold either way
        await Drain(
            gameHub.StreamAsync<string>(
                "ResolveEvadeEncounterAction",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        var persistedEncounter = await GetEncounter(encounter.Id);
        Assert.Equal(EncounterState.Completed, persistedEncounter.State);

        var fight = await FindFight(_playerId);
        if (fight != null)
        {
            Assert.Equal(
                new[] { _playerId, monster.Id }.OrderBy(id => id),
                fight.CombatantIds.OrderBy(id => id)
            );
        }
    }

    [Fact]
    public async Task ResolveEncounterAction_Retreat_MovesThePlayerBack_OnlyWhenNoFightStarts()
    {
        // Arrange
        var originLocation = Builders.MakeLocation(_worldId, _stateId);
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            context.Locations.Add(originLocation);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (faction, monster) = await SeedHostileGroup();
        await SeedActiveEncounter(faction, monster, originLocation.Id);

        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act — the retreat roll is random, so this asserts the invariant that holds either way
        await Drain(
            gameHub.StreamAsync<string>(
                "ResolveRetreatEncounterAction",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        var player = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _playerId,
            TestContext.Current.CancellationToken
        );
        var fight = await FindFight(_playerId);

        if (fight == null)
        {
            Assert.Equal(originLocation.Id, player.LocationId);
        }
        else
        {
            Assert.Equal(_locationId, player.LocationId);
        }
    }

    [Fact]
    public async Task StartTheftEncounterNarration_NarratesThenPublishesTheEncounter()
    {
        // Arrange
        var (encounter, owner) = await SeedActiveTheftEncounter();
        var sessionId = await StartSession();
        await using var connection = fixture.CreateHubConnection(sessionId);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        var encounterStarted = new TaskCompletionSource<TheftEncounterState>();
        connection.Register<IGameClient>(
            new TestGameClient
            {
                Connection = connection,
                OnTheftEncounterStarted = state => encounterStarted.TrySetResult(state),
            }
        );

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
        Assert.Equal(owner.Name, state.ConfrontingName);
        Assert.Equal(["Silver Ring"], state.ItemNames);
        Assert.Equal(["Apologize", "Fight"], state.AllowedActions);
    }

    [Fact]
    public async Task StartTheftEncounterNarration_ReturnsAMessageWithoutPublishing_WhenEncounterDoesNotMatch()
    {
        // Arrange
        await SeedActiveTheftEncounter();
        var sessionId = await StartSession();
        await using var connection = fixture.CreateHubConnection(sessionId);

        var initialEncounterStarted = new TaskCompletionSource();
        var started = 0;
        connection.Register<IGameClient>(
            new TestGameClient
            {
                Connection = connection,
                OnTheftEncounterStarted = _ =>
                {
                    Interlocked.Increment(ref started);
                    initialEncounterStarted.TrySetResult();
                },
            }
        );
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await initialEncounterStarted.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        Interlocked.Exchange(ref started, 0);

        // Act
        var narration = await Drain(
            connection.StreamAsync<string>(
                "StartTheftEncounterNarration",
                Guid.NewGuid(),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal("There's no theft encounter to resolve right now.", narration);
        Assert.Equal(0, started);
    }

    [Fact]
    public async Task Reconnect_PushesTheftEncounterStarted_WhenPlayerHasAnActiveTheftEncounter()
    {
        // Arrange
        var (encounter, owner) = await SeedActiveTheftEncounter();
        var sessionId = await StartSession();
        var encounterStarted = new TaskCompletionSource<TheftEncounterState>();
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
    }

    [Fact]
    public async Task ResolveApologizeTheftEncounterAction_CompletesTheEncounter()
    {
        // Arrange
        var (encounter, _) = await SeedActiveTheftEncounter();
        var sessionId = await StartSession();
        await using var connection = fixture.CreateHubConnection(sessionId);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        var encounterResolved = new TaskCompletionSource<TheftEncounterResolutionFact>();
        var sceneUpdated = new TaskCompletionSource<SceneSnapshot>();
        connection.Register<IGameClient>(
            new TestGameClient
            {
                Connection = connection,
                OnTheftEncounterResolved = fact => encounterResolved.TrySetResult(fact),
                OnSceneSnapshot = scene => sceneUpdated.TrySetResult(scene),
            }
        );

        // Act
        var narration = await Drain(
            connection.StreamAsync<string>(
                "ResolveApologizeTheftEncounterAction",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal(fixture.ChatClient.ChatResponseText, narration);
        var resolution = await encounterResolved.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(encounter.Id, resolution.EncounterId);
        Assert.Equal(TheftEncounterResolutionOutcome.Apologized, resolution.Outcome);
        await sceneUpdated.Task.WaitAsync(PushTimeout, TestContext.Current.CancellationToken);

        var persistedEncounter = await GetEncounter(encounter.Id);
        Assert.Equal(EncounterState.Completed, persistedEncounter.State);
    }

    [Fact]
    public async Task ResolveFightTheftEncounterAction_CompletesTheEncounterAndStartsAFight()
    {
        // Arrange
        var (encounter, owner) = await SeedActiveTheftEncounter();
        var sessionId = await StartSession();
        await using var connection = fixture.CreateHubConnection(sessionId);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        var encounterResolved = new TaskCompletionSource<TheftEncounterResolutionFact>();
        connection.Register<IGameClient>(
            new TestGameClient
            {
                Connection = connection,
                OnTheftEncounterResolved = fact => encounterResolved.TrySetResult(fact),
            }
        );

        // Act
        await Drain(
            connection.StreamAsync<string>(
                "ResolveFightTheftEncounterAction",
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        var resolution = await encounterResolved.Task.WaitAsync(
            PushTimeout,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(TheftEncounterResolutionOutcome.Fought, resolution.Outcome);

        var persistedEncounter = await GetEncounter(encounter.Id);
        Assert.Equal(EncounterState.Completed, persistedEncounter.State);

        var fight = await FindFight(_playerId);
        Assert.NotNull(fight);
        Assert.Equal(
            new[] { _playerId, owner.Id }.OrderBy(id => id),
            fight.CombatantIds.OrderBy(id => id)
        );
    }
}
