using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class EvaluateEncountersCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private EvaluateEncountersCommandHandler _handler = null!;
    private readonly Location _location = Builders.MakeLocation(WorldId, Guid.NewGuid());
    private readonly Creature _player = Builders.MakeCreature(WorldId, level: 1);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<EvaluateEncountersCommandHandler>();

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
    public async Task Handle_ReturnsNoEncounters_WhenNothingIsAtTheLocation()
    {
        // Act
        var result = await _handler.Handle(
            new EvaluateEncountersCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result.HostileEncounter);
        Assert.Null(result.GuardEncounter);
    }

    [Fact]
    public async Task Handle_CreatesHostileEncounter_WhenOneEligibleGroupEngages()
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
            new EvaluateEncountersCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result.HostileEncounter);
        Assert.Null(result.GuardEncounter);
    }

    [Fact]
    public async Task Handle_ReturnsNoEncounters_WhenPlayerAlreadyHasAnActiveHostileEncounter()
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
            new EvaluateEncountersCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result.HostileEncounter);
        Assert.Null(result.GuardEncounter);
    }

    [Fact]
    public async Task Handle_ReturnsNoEncounters_WhenPlayerAlreadyHasAnActiveGuardEncounter()
    {
        // Arrange
        var guard = Builders.MakeCreature(WorldId, locationId: _location.Id, level: 1);
        var existingEncounter = Builders.MakeGuardEncounter(
            WorldId,
            _player.Id,
            _location.Id,
            guard.Id
        );
        _context.Creatures.Add(guard);
        _context.Encounters.Add(existingEncounter);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new EvaluateEncountersCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result.HostileEncounter);
        Assert.Null(result.GuardEncounter);
    }

    [Fact]
    public async Task Handle_ReturnsNoEncounters_WhenPlayerAlreadyHasAnActiveFight()
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
            new EvaluateEncountersCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result.HostileEncounter);
        Assert.Null(result.GuardEncounter);
    }
}
