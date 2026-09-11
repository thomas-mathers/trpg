using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Application.GameTurns;
using TRPG.Application.Inventory;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.GameTurns.Tools;
using TRPG.Tests.Helpers;
using TRPG.Tools;

namespace TRPG.Tests.Tools;

[Collection("Database")]
public sealed class MoveToolTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _stateId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private MoveTool _tool = null!;
    private TestGameClientEventSink _eventSink = null!;
    private Location _oldLocation = null!;
    private Location _newLocation = null!;
    private Creature _player = null!;
    private Creature _guard = null!;
    private Faction _cityFaction = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["EncounterChance"] = 1f.ToString(CultureInfo.InvariantCulture),
                }
            )
            .Build();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .Configure<GuardEncounterOptions>(configuration)
            .BuildServiceProvider();
        _tool = _serviceProvider.GetRequiredService<MoveTool>();
        _eventSink = _serviceProvider.GetRequiredService<TestGameClientEventSink>();

        _oldLocation = Builders.MakeLocation(WorldId, _stateId);
        _newLocation = Builders.MakeLocation(WorldId, _stateId);
        _player = Builders.MakeCreature(WorldId, locationId: _oldLocation.Id);
        _guard = Builders.MakeCreature(
            WorldId,
            profession: Profession.Guard,
            locationId: _newLocation.Id
        );
        _cityFaction = Builders.MakeFaction(WorldId, isCityFaction: true);
        var connector = Builders.MakeLocationConnector(
            _oldLocation.Id,
            destinationLocationId: _newLocation.Id,
            destinationLabel: "Elsewhere"
        );
        var session = Builders.MakeGameSession(WorldId, _player.Id);
        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: _stateId);

        _context.States.Add(state);
        _context.Locations.AddRange(_oldLocation, _newLocation);
        _context.Creatures.AddRange(_player, _guard);
        _context.Factions.Add(_cityFaction);
        _context.FactionMembers.Add(
            Builders.MakeFactionMember(WorldId, _cityFaction.Id, _guard.Id)
        );
        _context.Reputations.Add(
            new Reputation
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                TargetId = _cityFaction.Id,
                TargetType = ReputationTargetType.Faction,
                Score = -50,
            }
        );
        _context.LocationConnectors.Add(connector);
        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var turnContext = _serviceProvider.GetRequiredService<GameTurnContext>();
        turnContext.PlayerId = _player.Id;
        turnContext.WorldId = WorldId;
        turnContext.SessionId = session.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Invoke_MarksTheFineAffordable_WhenThePlayerHasEnoughGold()
    {
        // Arrange
        var gold = Builders.MakeGold(WorldId, quantity: 10_000);
        gold.Ownership.OwnerId = _player.Id;
        gold.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(gold);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        await invoke("Elsewhere", TestContext.Current.CancellationToken);

        // Assert
        var startedEvent = Assert.Single(
            _eventSink.EnqueuedEvents.OfType<GuardEncounterStartedEvent>()
        );
        Assert.True(startedEvent.CanAffordFine);
    }

    [Fact]
    public async Task Invoke_MarksTheFineUnaffordable_WhenThePlayerLacksGold()
    {
        // Arrange
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        await invoke("Elsewhere", TestContext.Current.CancellationToken);

        // Assert
        var startedEvent = Assert.Single(
            _eventSink.EnqueuedEvents.OfType<GuardEncounterStartedEvent>()
        );
        Assert.False(startedEvent.CanAffordFine);
    }

    [Fact]
    public async Task Invoke_StartsTheInterceptingEncounter_WithoutMoving_WhenTheRoomKeyIsOverdue()
    {
        // Arrange
        var innkeeperName = await SeedOverdueInnBookingAtPlayerLocation();
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        var result = await invoke("Elsewhere", TestContext.Current.CancellationToken);

        // Assert — the interception replaces the move, so arrival encounters never get evaluated
        var moveResult = Assert.IsType<MoveToolResult>(result);
        Assert.NotNull(moveResult.OverdueRoomKeyEncounter);
        Assert.Equal(innkeeperName, moveResult.OverdueRoomKeyEncounter.ConfrontingName);
        Assert.Null(moveResult.GuardEncounter);

        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_oldLocation.Id, player!.LocationId);
    }

    [Fact]
    public async Task Invoke_ReturnsError_WithoutMoving_WhenPlayerHasAnActiveEncounter()
    {
        // Arrange
        var activeEncounter = Builders.MakeHostileEncounter(WorldId, _player.Id, _oldLocation.Id);
        _context.Encounters.Add(activeEncounter);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        var result = await invoke("Elsewhere", TestContext.Current.CancellationToken);

        // Assert
        var error = Assert.IsType<ToolError>(result);
        Assert.Contains("encounter", error.Error, StringComparison.OrdinalIgnoreCase);

        await using var verifyContext = db.CreateContext();
        var movedPlayer = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_oldLocation.Id, movedPlayer!.LocationId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invoke_InterruptsDeparture_WhenAGuardConfrontsThePlayer(bool returning)
    {
        // Arrange
        _guard.LocationId = _oldLocation.Id;
        _player.PreviousLocationId = returning ? _newLocation.Id : null;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        var result = await invoke("Elsewhere", TestContext.Current.CancellationToken);

        // Assert
        var moveResult = Assert.IsType<MoveToolResult>(result);
        Assert.NotNull(moveResult.GuardEncounter);
        Assert.Single(_eventSink.EnqueuedEvents.OfType<GuardEncounterStartedEvent>());
        Assert.False(_serviceProvider.GetRequiredService<GameTurnContext>().PlayerMoved);
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_oldLocation.Id, player!.LocationId);
        Assert.Equal(returning ? _newLocation.Id : (Guid?)null, player.PreviousLocationId);
        var encounter = await verifyContext.Encounters.SingleAsync(
            e => e.PlayerId == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_oldLocation.Id, encounter.LocationId);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(150, false)]
    [InlineData(150, true)]
    public async Task Invoke_EvaluatesDepartureBeforeArrival_WhenAGroupIsPresent(
        int aggression,
        bool returning
    )
    {
        // Arrange
        _player.PreviousLocationId = returning ? _newLocation.Id : null;
        var faction = Builders.MakeFaction(WorldId, aggression: aggression);
        var monster = Builders.MakeCreature(
            WorldId,
            locationId: _oldLocation.Id,
            level: _player.Level
        );
        var group = Builders.MakeEncounterGroup(WorldId, _oldLocation.Id, faction.Id);
        _context.Factions.Add(faction);
        _context.Creatures.Add(monster);
        _context.EncounterGroups.Add(group);
        _context.EncounterGroupMembers.Add(
            Builders.MakeEncounterGroupMember(WorldId, group.Id, monster.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        var result = await invoke("Elsewhere", TestContext.Current.CancellationToken);

        // Assert
        var moveResult = Assert.IsType<MoveToolResult>(result);
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        if (aggression > 0)
        {
            Assert.NotNull(moveResult.HostileEncounter);
            Assert.Null(moveResult.GuardEncounter);
            Assert.Equal(_oldLocation.Id, player!.LocationId);
            Assert.Single(_eventSink.EnqueuedEvents.OfType<HostileEncounterStartedEvent>());
        }
        else
        {
            Assert.Null(moveResult.HostileEncounter);
            Assert.NotNull(moveResult.GuardEncounter);
            Assert.Equal(_newLocation.Id, player!.LocationId);
            Assert.True(_serviceProvider.GetRequiredService<GameTurnContext>().PlayerMoved);
        }
    }

    [Fact]
    public async Task Invoke_IgnoresUnresolvedTrap_WhenLeavingItsLocation()
    {
        // Arrange
        var trap = Builders.MakeTrigger(
            WorldId,
            locationId: _oldLocation.Id,
            targetId: _newLocation.Id,
            trapKind: TrapKind.Mechanical
        );
        _context.Props.Add(trap);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        var result = await invoke("Elsewhere", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(Assert.IsType<MoveToolResult>(result).GuardEncounter);
        Assert.Empty(_eventSink.EnqueuedEvents.OfType<TrapEncounterStartedEvent>());
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_newLocation.Id, player!.LocationId);
        var persistedTrap = await verifyContext
            .Props.OfType<Trigger>()
            .SingleAsync(t => t.Id == trap.Id, TestContext.Current.CancellationToken);
        Assert.False(persistedTrap.IsResolved);
    }

    [Fact]
    public async Task Invoke_StartsTrapEncounter_WhenArrivingAtUnresolvedTrap()
    {
        // Arrange
        _guard.State = CreatureState.Dead;
        var trap = Builders.MakeTrigger(
            WorldId,
            locationId: _newLocation.Id,
            targetId: _oldLocation.Id,
            trapKind: TrapKind.Mechanical
        );
        _context.Props.Add(trap);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        await invoke("Elsewhere", TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(_eventSink.EnqueuedEvents.OfType<TrapEncounterStartedEvent>());
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_newLocation.Id, player!.LocationId);
        var encounter = await verifyContext
            .Encounters.OfType<TrapEncounter>()
            .SingleAsync(e => e.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.Equal(trap.Id, encounter.TriggerId);
    }

    [Fact]
    public async Task Invoke_DoesNotEvaluateDeparture_WhenDestinationIsInvalid()
    {
        // Arrange
        _guard.LocationId = _oldLocation.Id;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        var result = await invoke("Missing exit", TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<ToolError>(result);
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext.Encounters.AnyAsync(
                e => e.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_BypassesDepartureConfrontation_WhenRelocatingDirectly()
    {
        // Arrange
        _guard.LocationId = _oldLocation.Id;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var movePlayer = _serviceProvider.GetRequiredService<MovePlayerCommandHandler>();

        // Act
        await movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = _player.Id,
                DestinationLocationId = _newLocation.Id,
                Playtime = TimeSpan.Zero,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_newLocation.Id, player!.LocationId);
        Assert.False(
            await verifyContext.Encounters.AnyAsync(
                e => e.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    private async Task<string> SeedOverdueInnBookingAtPlayerLocation()
    {
        var inn = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.Inn);
        var lobby = Builders.MakeRoom(
            inn.Id,
            worldId: WorldId,
            locationId: _oldLocation.Id,
            name: "Lobby"
        );
        var innkeeper = Builders.MakeCreature(worldId: WorldId, locationId: _oldLocation.Id);
        var counter = Builders.MakeWorkstation(
            worldId: WorldId,
            locationId: _oldLocation.Id,
            ownerCreatureId: innkeeper.Id
        );

        var guestRoom = Builders.MakeRoom(inn.Id, worldId: WorldId, name: "North Guest Room");
        var guestRoomLocation = Builders.MakeLocation(
            WorldId,
            _stateId,
            roomId: guestRoom.Id,
            id: guestRoom.LocationId
        );
        var key = Builders.MakeKey(
            WorldId,
            quantity: 1,
            ownerId: _player.Id,
            ownerType: OwnerType.Creature
        );
        var booking = Builders.MakeRoomBooking(
            WorldId,
            guestRoom.Id,
            key.Id,
            _player.Id,
            dueAtPlaytime: TimeSpan.Zero
        );

        _context.Buildings.Add(inn);
        _context.Rooms.AddRange(lobby, guestRoom);
        _context.Locations.Add(guestRoomLocation);
        _context.Creatures.Add(innkeeper);
        _context.Props.Add(counter);
        _context.Items.Add(key);
        _context.RoomBookings.Add(booking);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return innkeeper.Name;
    }

    [Fact]
    public async Task Invoke_ReturnsError_WithoutMoving_WhenPlayerHasAnActiveFight()
    {
        // Arrange
        var activeFight = Builders.MakeFight(WorldId, _player.Id, [_player.Id]);
        _context.Encounters.Add(activeFight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        var result = await invoke("Elsewhere", TestContext.Current.CancellationToken);

        // Assert
        var error = Assert.IsType<ToolError>(result);
        Assert.Contains("encounter", error.Error, StringComparison.OrdinalIgnoreCase);

        await using var verifyContext = db.CreateContext();
        var movedPlayer = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_oldLocation.Id, movedPlayer!.LocationId);
    }
}
