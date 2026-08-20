using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters.Events;
using TRPG.Application.GameTurns;
using TRPG.Application.Inventory;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.GameTurns.Tools;
using TRPG.Tests.Helpers;

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
}
