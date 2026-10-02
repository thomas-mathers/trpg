using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Configuration;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.GameTurns.Commands;

public sealed class ExecutePlayerMoveCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ExecutePlayerMoveCommandHandler _handler = null!;
    private Location _origin = null!;
    private Location _destination = null!;
    private LocationConnector _connector = null!;
    private Creature _player = null!;
    private GameSession _session = null!;

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
        _handler = _serviceProvider.GetRequiredService<ExecutePlayerMoveCommandHandler>();

        var stateId = Guid.NewGuid();
        _origin = Builders.MakeLocation(_worldId, stateId);
        _destination = Builders.MakeLocation(_worldId, stateId);
        _player = Builders.MakeCreature(_worldId, locationId: _origin.Id);
        _connector = Builders.MakeLocationConnector(
            _origin.Id,
            destinationLocationId: _destination.Id,
            destinationLabel: "Elsewhere",
            worldId: _worldId
        );
        _session = Builders.MakeGameSession(_worldId, _player.Id);

        _context.Worlds.Add(Builders.MakeWorld(_worldId));
        _context.States.Add(Builders.MakeState(Guid.NewGuid(), worldId: _worldId, id: stateId));
        _context.Locations.AddRange(_origin, _destination);
        _context.Creatures.Add(_player);
        _context.LocationConnectors.Add(_connector);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_MovesThePlayerAndReturnsTheDestinationScene()
    {
        var result = Assert.IsType<MoveCompletedResult>(await Execute(_connector.Id));

        Assert.Equal(_destination.Id, result.Scene.LocationId);
        Assert.Null(result.Encounter);
        await using var verifyContext = db.CreateContext();
        var player = await GetPlayer(verifyContext);
        Assert.Equal(_destination.Id, player.LocationId);
    }

    [Fact]
    public async Task Handle_RejectsAnUnknownDestinationBeforeMoving()
    {
        var result = Assert.IsType<MoveRejectedResult>(await Execute(Guid.NewGuid()));

        Assert.Equal(EntryOutcome.DestinationNotFound, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var player = await GetPlayer(verifyContext);
        Assert.Equal(_origin.Id, player.LocationId);
    }

    [Fact]
    public async Task Handle_RejectsALockedConnectorBeforeMoving()
    {
        var door = Builders.MakeDoorConnector(_connector.Id, isLocked: true, worldId: _worldId);
        var key = Builders.MakeKey(_worldId, quantity: 1);
        _context.DoorConnectors.Add(door);
        _context.Items.Add(key);
        _context.DoorConnectorKeys.Add(Builders.MakeDoorConnectorKey(key.Id, door.Id, _worldId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<MoveRejectedResult>(await Execute(_connector.Id));

        Assert.Equal(EntryOutcome.Locked, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var player = await GetPlayer(verifyContext);
        Assert.Equal(_origin.Id, player.LocationId);
    }

    [Fact]
    public async Task Handle_RejectsMovementDuringAnActiveEncounter()
    {
        _context.Encounters.Add(Builders.MakeHostileEncounter(_worldId, _player.Id, _origin.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<MoveRejectedResult>(await Execute(_connector.Id));

        Assert.Equal(EntryOutcome.EncounterActive, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var player = await GetPlayer(verifyContext);
        Assert.Equal(_origin.Id, player.LocationId);
    }

    [Fact]
    public async Task Handle_ReturnsTheDepartureInterceptionWithoutMoving()
    {
        var faction = Builders.MakeFaction(_worldId, aggression: 150);
        var creature = Builders.MakeCreature(_worldId, locationId: _origin.Id);
        var group = Builders.MakeEncounterGroup(_worldId, _origin.Id, faction.Id);
        _context.Factions.Add(faction);
        _context.Creatures.Add(creature);
        _context.EncounterGroups.Add(group);
        _context.EncounterGroupMembers.Add(
            Builders.MakeEncounterGroupMember(_worldId, group.Id, creature.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<MoveInterruptedResult>(await Execute(_connector.Id));

        Assert.IsType<HostileEncounter>(result.Encounter);
        Assert.Equal(_origin.Id, result.Scene.LocationId);
        await using var verifyContext = db.CreateContext();
        var player = await GetPlayer(verifyContext);
        Assert.Equal(_origin.Id, player.LocationId);
    }

    [Fact]
    public async Task Handle_ReturnsTheArrivalEncounterAfterRelocation()
    {
        var trap = Builders.MakeTrap(
            _worldId,
            locationId: _destination.Id,
            targetId: _origin.Id,
            trapKind: TrapKind.Mechanical
        );
        _context.Props.Add(trap);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = Assert.IsType<MoveCompletedResult>(await Execute(_connector.Id));

        Assert.IsType<TrapEncounter>(result.Encounter);
        Assert.Equal(_destination.Id, result.Scene.LocationId);
        await using var verifyContext = db.CreateContext();
        var player = await GetPlayer(verifyContext);
        Assert.Equal(_destination.Id, player.LocationId);
    }

    [Fact]
    public async Task Handle_CatchesUpTheDestinationBeforeEvaluatingArrivalEncounters()
    {
        var faction = Builders.MakeFaction(
            _worldId,
            aggression: 100,
            creatureType: CreatureType.Beast
        );
        var spawner = Builders.MakeCreatureSpawner(
            _worldId,
            _destination.Id,
            archetypeCreatureTypes: [CreatureType.Beast]
        );
        _context.Factions.Add(faction);
        _context.CreatureSpawners.Add(spawner);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _context
            .Worlds.Where(world => world.Id == _worldId)
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        world => world.GameTime,
                        GameClock.Epoch + TimeSpan.FromHours(48)
                    ),
                TestContext.Current.CancellationToken
            );

        var result = Assert.IsType<MoveCompletedResult>(await Execute(_connector.Id));

        Assert.IsType<HostileEncounter>(result.Encounter);
        await using var verifyContext = db.CreateContext();
        Assert.True(
            await verifyContext.Creatures.AnyAsync(
                creature => creature.SpawnerId == spawner.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_LeavesTheWorldClockAlone_WhenTheConnectorHasATravelDistance()
    {
        // Arrange
        _context.TravelConnectors.Add(
            Builders.MakeTravelConnector(_connector.Id, distance: 116, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await Execute(_connector.Id);

        // Assert
        await using var verifyContext = db.CreateContext();
        var world = await verifyContext.Worlds.SingleAsync(
            world => world.Id == _worldId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(GameClock.Epoch, world.GameTime);
    }

    private Task<ExecutePlayerMoveResult> Execute(Guid connectorId) =>
        _handler.Handle(
            new ExecutePlayerMoveCommand
            {
                SessionId = _session.Id,
                WorldId = _worldId,
                PlayerId = _player.Id,
                ConnectorId = connectorId,
            },
            TestContext.Current.CancellationToken
        );

    private async Task<Creature> GetPlayer(TrpgDbContext context) =>
        await context.Creatures.SingleAsync(
            creature => creature.Id == _player.Id,
            TestContext.Current.CancellationToken
        );
}
