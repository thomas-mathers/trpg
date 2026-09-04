using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns;
using TRPG.Combat.Tools;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Tools;

[Collection("Database")]
public sealed class StartFightToolTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private StartFightTool _tool = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _tool = _serviceProvider.GetRequiredService<StartFightTool>();
        var turnContext = _serviceProvider.GetRequiredService<GameTurnContext>();
        turnContext.PlayerId = _player.Id;
        turnContext.WorldId = WorldId;

        var session = Builders.MakeGameSession(WorldId, _player.Id);
        turnContext.SessionId = session.Id;
        _context.Creatures.Add(_player);
        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Invoke_StartsFight_WhenTargetIsHumanoid()
    {
        // Arrange
        var target = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Human,
            locationId: LocationId,
            name: "Tavern Keeper"
        );
        _context.Creatures.Add(target);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        await invoke(target.Name, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var fight = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(
                fight => fight.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Contains(target.Id, fight.CombatantIds);
    }

    [Fact]
    public async Task Invoke_StartsAFightWithASurpriseRound_WhenTheTargetIsSleeping()
    {
        // Arrange
        var target = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: LocationId,
            name: "Sleeping Wolf",
            state: CreatureState.Sleeping
        );
        _context.Creatures.Add(target);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        await invoke(target.Name, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var fight = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(
                fight => fight.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.True(fight.HasSurpriseRound);
    }

    [Fact]
    public async Task Invoke_StartsAFightWithASurpriseRound_WhenThePlayerIsSneaking()
    {
        // Arrange
        var target = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: LocationId,
            name: "Alert Wolf",
            state: CreatureState.Idle
        );
        _context.Creatures.Add(target);
        _player.IsSneaking = true;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        await invoke(target.Name, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var fight = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(
                fight => fight.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.True(fight.HasSurpriseRound);
    }

    [Fact]
    public async Task Invoke_StartsAFightWithoutASurpriseRound_WhenNeitherSneakingNorTargetAsleep()
    {
        // Arrange
        var target = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: LocationId,
            name: "Alert Wolf",
            state: CreatureState.Idle
        );
        _context.Creatures.Add(target);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        await invoke(target.Name, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var fight = await verifyContext
            .Encounters.OfType<FightEncounter>()
            .SingleAsync(
                fight => fight.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            );
        Assert.False(fight.HasSurpriseRound);
    }

    [Fact]
    public async Task Invoke_AlertsTheWholeGroup_WhenAtLeastOneMemberIsAwake()
    {
        // Arrange
        var target = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: LocationId,
            name: "Awake Goblin",
            state: CreatureState.Idle
        );
        var sleepingPackmate = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: LocationId,
            name: "Sleeping Goblin",
            state: CreatureState.Sleeping
        );
        await SeedGroup(target, sleepingPackmate);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        await invoke(target.Name, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedTarget = await verifyContext.Creatures.FindAsync(
            [target.Id],
            TestContext.Current.CancellationToken
        );
        var updatedPackmate = await verifyContext.Creatures.FindAsync(
            [sleepingPackmate.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Alerted, updatedTarget!.State);
        Assert.Equal(CreatureState.Alerted, updatedPackmate!.State);
    }

    [Fact]
    public async Task Invoke_AlertsOnlyTheTarget_WhenTheRestOfTheGroupIsAsleep()
    {
        // Arrange
        var target = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: LocationId,
            name: "Sleeping Goblin Scout",
            state: CreatureState.Sleeping
        );
        var sleepingPackmate = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: LocationId,
            name: "Sleeping Goblin Guard",
            state: CreatureState.Sleeping
        );
        await SeedGroup(target, sleepingPackmate);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        await invoke(target.Name, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedTarget = await verifyContext.Creatures.FindAsync(
            [target.Id],
            TestContext.Current.CancellationToken
        );
        var updatedPackmate = await verifyContext.Creatures.FindAsync(
            [sleepingPackmate.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Alerted, updatedTarget!.State);
        Assert.Equal(CreatureState.Sleeping, updatedPackmate!.State);
    }

    private async Task SeedGroup(params Creature[] members)
    {
        var faction = Builders.MakeFaction(WorldId);
        var group = Builders.MakeEncounterGroup(WorldId, LocationId, faction.Id);
        _context.Factions.Add(faction);
        _context.Creatures.AddRange(members);
        _context.EncounterGroups.Add(group);
        _context.EncounterGroupMembers.AddRange(
            members.Select(member =>
                Builders.MakeEncounterGroupMember(WorldId, group.Id, member.Id)
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
