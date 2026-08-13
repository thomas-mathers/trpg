using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Contracts.Encounters.Requests;
using TRPG.Contracts.GameSessions.Responses;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.GameSessions.Requests;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Hubs;

[Collection("Endpoints")]
public sealed class ResolveEncounterActionTests(EndpointTestFixture fixture) : IAsyncLifetime
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
                "ResolveEncounterAction",
                new AttackEncounterAction(),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Equal("There's no encounter to resolve right now.", narration);
    }

    private async Task<(Faction Faction, Creature Monster, EncounterGroup Group)> SeedHostileGroup()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var faction = Builders.MakeFaction(_worldId, aggression: 150);
        var monster = Builders.MakeCreature(
            _worldId,
            creatureType: CreatureType.Beast,
            locationId: _locationId,
            level: 1
        );
        var group = Builders.MakeEncounterGroup(_worldId, _locationId, faction.Id);
        var member = Builders.MakeEncounterGroupMember(_worldId, group.Id, monster.Id);
        context.Factions.Add(faction);
        context.Creatures.Add(monster);
        context.EncounterGroups.Add(group);
        context.EncounterGroupMembers.Add(member);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (faction, monster, group);
    }

    private async Task<HostileEncounter> SeedActiveEncounter(Guid encounterGroupId)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();

        var encounter = Builders.MakeHostileEncounter(
            _worldId,
            _playerId,
            _locationId,
            encounterGroupId
        );
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return encounter;
    }

    private async Task<Data.Models.Encounter> GetEncounter(Guid encounterId)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        return await context.Encounters.SingleAsync(
            e => e.Id == encounterId,
            TestContext.Current.CancellationToken
        );
    }

    private async Task<Fight?> FindFight(Guid playerId)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
        return await context.Fights.SingleOrDefaultAsync(
            f => f.PlayerId == playerId,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ResolveEncounterAction_Attack_CompletesTheEncounter_AndStartsAFight_WithOnlyTheGroupsMembers()
    {
        // Arrange
        var (_, monster, group) = await SeedHostileGroup();
        var encounter = await SeedActiveEncounter(group.Id);
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act
        var narration = await Drain(
            gameHub.StreamAsync<string>(
                "ResolveEncounterAction",
                new AttackEncounterAction(),
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
        Assert.Equal(encounter.Id, fight.EncounterId);
        Assert.Equal(
            new[] { _playerId, monster.Id }.OrderBy(id => id),
            fight.CombatantIds.OrderBy(id => id)
        );
    }

    [Fact]
    public async Task ResolveEncounterAction_Evade_AlwaysCompletesTheEncounter_AndConsistentlyLinksAnyFightThatStarts()
    {
        // Arrange
        var (_, monster, group) = await SeedHostileGroup();
        var encounter = await SeedActiveEncounter(group.Id);
        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act — the evade roll is random, so this asserts the invariants that hold either way
        await Drain(
            gameHub.StreamAsync<string>(
                "ResolveEncounterAction",
                new EvadeEncounterAction(),
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        var persistedEncounter = await GetEncounter(encounter.Id);
        Assert.Equal(EncounterState.Completed, persistedEncounter.State);

        var fight = await FindFight(_playerId);
        if (fight != null)
        {
            Assert.Equal(encounter.Id, fight.EncounterId);
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

        var (_, _, group) = await SeedHostileGroup();
        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrpgDbContext>();
            var encounter = Builders.MakeHostileEncounter(
                _worldId,
                _playerId,
                _locationId,
                group.Id,
                arrivalOriginLocationId: originLocation.Id
            );
            context.Encounters.Add(encounter);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sessionId = await StartSession();
        await using var gameHub = await Connect(sessionId);

        // Act — the retreat roll is random, so this asserts the invariant that holds either way
        await Drain(
            gameHub.StreamAsync<string>(
                "ResolveEncounterAction",
                new RetreatEncounterAction(),
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
}
