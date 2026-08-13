using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Encounters.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Encounters.Commands;

[Collection("Database")]
public sealed class EvaluateLocationEncountersCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private EvaluateLocationEncountersCommandHandler _handler = null!;
    private readonly Location _location = Builders.MakeLocation(WorldId, Guid.NewGuid());
    private readonly Creature _player = Builders.MakeCreature(WorldId, level: 1);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<EvaluateLocationEncountersCommandHandler>();

        _player.LocationId = _location.Id;
        _context.Locations.Add(_location);
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoGroupsAreAtTheLocation()
    {
        // Act
        var result = await _handler.Handle(
            new EvaluateLocationEncountersCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_CreatesAndReturnsAnEncounter_WhenOneEligibleGroupEngages()
    {
        // Arrange
        var faction = Builders.MakeFaction(WorldId, aggression: 150);
        var monster = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: _location.Id,
            level: 1
        );
        var group = Builders.MakeEncounterGroup(WorldId, _location.Id, faction.Id);
        var member = Builders.MakeEncounterGroupMember(WorldId, group.Id, monster.Id);
        _context.Factions.Add(faction);
        _context.Creatures.Add(monster);
        _context.EncounterGroups.Add(group);
        _context.EncounterGroupMembers.Add(member);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new EvaluateLocationEncountersCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                OriginLocationId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(faction.Name, result.FactionName);
        Assert.Equal(monster.Name, Assert.Single(result.Members).Name);

        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext
            .Encounters.OfType<HostileEncounter>()
            .SingleAsync(e => e.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.Equal(group.Id, persisted.EncounterGroupId);
        Assert.Equal(EncounterState.Active, persisted.State);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenPlayerAlreadyHasAnActiveEncounter()
    {
        // Arrange
        var faction = Builders.MakeFaction(WorldId, aggression: 150);
        var monster = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: _location.Id,
            level: 1
        );
        var group = Builders.MakeEncounterGroup(WorldId, _location.Id, faction.Id);
        var member = Builders.MakeEncounterGroupMember(WorldId, group.Id, monster.Id);
        var existingEncounter = Builders.MakeHostileEncounter(
            WorldId,
            _player.Id,
            _location.Id,
            group.Id
        );
        _context.Factions.Add(faction);
        _context.Creatures.Add(monster);
        _context.EncounterGroups.Add(group);
        _context.EncounterGroupMembers.Add(member);
        _context.Encounters.Add(existingEncounter);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new EvaluateLocationEncountersCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);

        await using var verifyContext = db.CreateContext();
        var encounterCount = await verifyContext.Encounters.CountAsync(
            e => e.PlayerId == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, encounterCount);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenPlayerAlreadyHasAnActiveFight()
    {
        // Arrange
        var faction = Builders.MakeFaction(WorldId, aggression: 150);
        var monster = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: _location.Id,
            level: 1
        );
        var group = Builders.MakeEncounterGroup(WorldId, _location.Id, faction.Id);
        var member = Builders.MakeEncounterGroupMember(WorldId, group.Id, monster.Id);
        var fight = new Fight
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            CombatantIds = [_player.Id, monster.Id],
            StartedAt = DateTime.UtcNow,
        };
        _context.Factions.Add(faction);
        _context.Creatures.Add(monster);
        _context.EncounterGroups.Add(group);
        _context.EncounterGroupMembers.Add(member);
        _context.Fights.Add(fight);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new EvaluateLocationEncountersCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }
}
