using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

public sealed class EvaluateAmbientEncounterCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly TestChanceRoller _chanceRoller = new() { Result = true };

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private EvaluateAmbientEncounterCommandHandler _handler = null!;
    private TestGameClientEventSink _events = null!;
    private Location _location = null!;
    private Creature _player = null!;
    private Faction _faction = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IChanceRoller>(_chanceRoller)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<EvaluateAmbientEncounterCommandHandler>();
        _events = _serviceProvider.GetRequiredService<TestGameClientEventSink>();

        _location = Builders.MakeLocation(_worldId, kind: LocationKind.Wilderness);
        _player = Builders.MakeCreature(_worldId, locationId: _location.Id, level: 1);
        _faction = Builders.MakeFaction(_worldId, aggression: 100);
        _context.Locations.Add(_location);
        _context.Creatures.Add(_player);
        _context.Factions.Add(_faction);
        _context.GameSessions.Add(Builders.MakeGameSession(_worldId, _player.Id));
        _context.CreatureSkills.Add(
            Builders.MakeCreatureSkill(_player.Id, Skill.Sneak, level: 1, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<EncounterGroup> SeedSpawnedGroup()
    {
        var monster = Builders.MakeCreature(
            _worldId,
            creatureType: CreatureType.Beast,
            locationId: _location.Id,
            level: 1
        );
        var group = Builders.MakeEncounterGroup(_worldId, _location.Id, _faction.Id);
        _context.Creatures.Add(monster);
        _context.EncounterGroups.Add(group);
        _context.EncounterGroupMembers.Add(
            Builders.MakeEncounterGroupMember(_worldId, group.Id, monster.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return group;
    }

    private EvaluateAmbientEncounterCommand MakeCommand(params Guid[] spawnedGroupIds) =>
        new()
        {
            WorldId = _worldId,
            PlayerId = _player.Id,
            SpawnedGroupIds = spawnedGroupIds,
            GameTime = GameClock.Epoch,
        };

    [Fact]
    public async Task Handle_PublishesAHostileEncounterWithoutHarm_WhenASpawnedGroupEngages()
    {
        // Arrange
        var group = await SeedSpawnedGroup();

        // Act
        await _handler.Handle(MakeCommand(group.Id), TestContext.Current.CancellationToken);

        // Assert
        var started = Assert.Single(_events.EnqueuedEvents.OfType<HostileEncounterStartedEvent>());
        Assert.Equal(_player.Id, started.Encounter.PlayerId);
        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext
            .Creatures.Where(creature => creature.Id == _player.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(_player.CurrentHp, persisted.CurrentHp);
        Assert.True(persisted.IsEngaged);
        Assert.Empty(verifyContext.Encounters.OfType<FightEncounter>());
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoGroupSpawned()
    {
        // Arrange
        await SeedSpawnedGroup();

        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenTheGroupWasNotJustSpawned()
    {
        // Arrange
        await SeedSpawnedGroup();

        // Act
        await _handler.Handle(MakeCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenPlayerAlreadyHasAnActiveEncounter()
    {
        // Arrange
        var group = await SeedSpawnedGroup();
        var existing = Builders.MakeHostileEncounter(_worldId, _player.Id, _location.Id);
        _context.Encounters.Add(existing);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(MakeCommand(group.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
        await using var verifyContext = db.CreateContext();
        Assert.Equal(
            1,
            await verifyContext.Encounters.CountAsync(
                encounter => encounter.WorldId == _worldId,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenPlayerIsDead()
    {
        // Arrange
        var group = await SeedSpawnedGroup();
        await _context
            .Creatures.Where(creature => creature.Id == _player.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.State, CreatureState.Dead),
                TestContext.Current.CancellationToken
            );

        // Act
        await _handler.Handle(MakeCommand(group.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
    }

    [Fact]
    public async Task Handle_PublishesNothing_WhenEveryGroupMemberIsAsleep()
    {
        // Arrange
        var group = await SeedSpawnedGroup();
        await _context
            .Creatures.Where(creature => creature.Id != _player.Id && creature.WorldId == _worldId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(creature => creature.State, CreatureState.Sleeping),
                TestContext.Current.CancellationToken
            );

        // Act
        await _handler.Handle(MakeCommand(group.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_events.EnqueuedEvents);
    }
}
