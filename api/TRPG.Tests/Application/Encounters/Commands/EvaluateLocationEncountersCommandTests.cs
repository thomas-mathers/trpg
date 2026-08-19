using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

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
        Assert.Equal(faction.Id, persisted.FactionId);
        Assert.Equal(monster.Id, Assert.Single(persisted.Members).Id);
        Assert.Equal(EncounterState.Active, persisted.State);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenOnlyGroupMemberIsSleeping()
    {
        // Arrange
        var faction = Builders.MakeFaction(WorldId, aggression: 150);
        var monster = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: _location.Id,
            level: 1,
            state: CreatureState.Sleeping
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
            new EvaluateLocationEncountersCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ExcludesSleepingMembers_FromTheInitialEncounterRoster()
    {
        // Arrange
        var faction = Builders.MakeFaction(WorldId, aggression: 150);
        var awakeMonster = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: _location.Id,
            level: 1,
            state: CreatureState.Idle
        );
        var sleepingMonster = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: _location.Id,
            level: 1,
            state: CreatureState.Sleeping
        );
        var group = Builders.MakeEncounterGroup(WorldId, _location.Id, faction.Id);
        var awakeMember = Builders.MakeEncounterGroupMember(WorldId, group.Id, awakeMonster.Id);
        var sleepingMember = Builders.MakeEncounterGroupMember(
            WorldId,
            group.Id,
            sleepingMonster.Id
        );
        _context.Factions.Add(faction);
        _context.Creatures.AddRange(awakeMonster, sleepingMonster);
        _context.EncounterGroups.Add(group);
        _context.EncounterGroupMembers.AddRange(awakeMember, sleepingMember);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new EvaluateLocationEncountersCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(awakeMonster.Name, Assert.Single(result!.Members).Name);
    }
}
