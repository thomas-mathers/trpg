using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures;
using TRPG.Application.Encounters.Events;
using TRPG.Application.LocationSimulation;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SyncActiveLocationRoutinesCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();

    private readonly TestChanceRoller _chanceRoller = new() { Result = true };
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncActiveLocationRoutinesCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IChanceRoller>(_chanceRoller)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SyncActiveLocationRoutinesCommandHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_SyncsEveryDistinctLocation_WhenPlayersShareOneOfThem()
    {
        // Arrange
        var firstLocation = Builders.MakeLocation(_worldId, kind: LocationKind.Wilderness);
        var secondLocation = Builders.MakeLocation(_worldId, kind: LocationKind.Wilderness);
        var firstSleeper = Builders.MakeCreature(_worldId, locationId: firstLocation.Id);
        var secondSleeper = Builders.MakeCreature(_worldId, locationId: secondLocation.Id);
        _context.Locations.AddRange(firstLocation, secondLocation);
        _context.Creatures.AddRange(firstSleeper, secondSleeper);
        _context.CreatureJobs.AddRange(
            MakeSleepJob(firstSleeper.Id, firstLocation.Id),
            MakeSleepJob(secondSleeper.Id, secondLocation.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SyncActiveLocationRoutinesCommand
            {
                WorldId = _worldId,
                GameTime = GameClock.Epoch,
                Players =
                [
                    new ActiveLocationPlayer(firstLocation.Id, Guid.NewGuid(), PlayerLevel: 1),
                    new ActiveLocationPlayer(firstLocation.Id, Guid.NewGuid(), PlayerLevel: 4),
                    new ActiveLocationPlayer(secondLocation.Id, Guid.NewGuid(), PlayerLevel: 2),
                ],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var states = await verifyContext
            .Creatures.Where(creature =>
                creature.Id == firstSleeper.Id || creature.Id == secondSleeper.Id
            )
            .Select(creature => creature.State)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, states.Length);
        Assert.All(states, state => Assert.Equal(CreatureState.Sleeping, state));
    }

    [Fact]
    public async Task Handle_PresentsAnAmbientEncounter_WhenSpawnerTriggersAtTheWatchedLocation()
    {
        // Arrange
        var player = await SeedWatchedPlayer(spawnerLastSyncGameTime: GameClock.Epoch);

        // Act
        await _handler.Handle(
            MakeWatchCommand(player, GameClock.Epoch + TimeSpan.FromHours(24)),
            TestContext.Current.CancellationToken
        );

        // Assert
        var started = Assert.Single(
            _serviceProvider
                .GetRequiredService<TestGameClientEventSink>()
                .EnqueuedEvents.OfType<HostileEncounterStartedEvent>()
        );
        Assert.Equal(player.Id, started.Encounter.PlayerId);
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext
                .Encounters.OfType<FightEncounter>()
                .Where(encounter => encounter.WorldId == _worldId)
                .AnyAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            player.CurrentHp,
            await verifyContext
                .Creatures.Where(creature => creature.Id == player.Id)
                .Select(creature => creature.CurrentHp)
                .SingleAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_PresentsNoEncounter_WhenSpawnerHasNotTriggered()
    {
        // Arrange
        var player = await SeedWatchedPlayer(spawnerLastSyncGameTime: GameClock.Epoch);

        // Act
        await _handler.Handle(
            MakeWatchCommand(player, GameClock.Epoch + TimeSpan.FromHours(12)),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(_serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents);
    }

    [Fact]
    public async Task Handle_DoesNotReevaluateExistingGroups_WhenNothingNewSpawns()
    {
        // Arrange
        var player = await SeedWatchedPlayer(spawnerLastSyncGameTime: GameClock.Epoch);
        var faction = await _context.Factions.SingleAsync(
            f => f.WorldId == _worldId && f.CreatureType == CreatureType.Beast,
            TestContext.Current.CancellationToken
        );
        var resident = Builders.MakeCreature(
            _worldId,
            creatureType: CreatureType.Beast,
            locationId: player.LocationId,
            level: 1
        );
        var group = Builders.MakeEncounterGroup(_worldId, player.LocationId, faction.Id);
        _context.Creatures.Add(resident);
        _context.EncounterGroups.Add(group);
        _context.EncounterGroupMembers.Add(
            Builders.MakeEncounterGroupMember(_worldId, group.Id, resident.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            MakeWatchCommand(player, GameClock.Epoch + TimeSpan.FromHours(12)),
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext
                .Encounters.Where(encounter => encounter.WorldId == _worldId)
                .AnyAsync(TestContext.Current.CancellationToken)
        );
    }

    private SyncActiveLocationRoutinesCommand MakeWatchCommand(
        Creature player,
        GameInstant gameTime
    ) =>
        new()
        {
            WorldId = _worldId,
            GameTime = gameTime,
            Players = [new ActiveLocationPlayer(player.LocationId, player.Id, player.Level)],
        };

    private async Task<Creature> SeedWatchedPlayer(GameInstant spawnerLastSyncGameTime)
    {
        var location = Builders.MakeLocation(_worldId, kind: LocationKind.Wilderness);
        var player = Builders.MakeCreature(_worldId, locationId: location.Id, level: 1);
        _context.Locations.Add(location);
        _context.Creatures.Add(player);
        _context.GameSessions.Add(Builders.MakeGameSession(_worldId, player.Id));
        _context.CreatureSkills.Add(
            Builders.MakeCreatureSkill(player.Id, Skill.Sneak, level: 1, worldId: _worldId)
        );
        _context.Factions.Add(
            Builders.MakeFaction(_worldId, aggression: 100, creatureType: CreatureType.Beast)
        );
        _context.CreatureSpawners.Add(
            Builders.MakeCreatureSpawner(
                _worldId,
                location.Id,
                maxPopulation: 1,
                lastSyncGameTime: spawnerLastSyncGameTime
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return player;
    }

    private CreatureJob MakeSleepJob(Guid creatureId, Guid locationId) =>
        Builders.MakeCreatureJob(
            creatureId,
            action: CreatureJobAction.Sleep,
            startHour: 6,
            endHour: 22,
            locationId: locationId,
            worldId: _worldId,
            priority: 100
        );
}
