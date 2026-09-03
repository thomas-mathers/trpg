using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class AttemptLockpickCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    // Instance field, not the usual shared static: SetTrespassingBuildingCommand/
    // GetTrespassingBuildingIdQuery treat GameSession as one-per-world, so a WorldId shared across
    // every [Fact] in this class would leave multiple GameSession rows under the same WorldId.
    private readonly Guid _worldId = Guid.NewGuid();

    private readonly Guid _exteriorLocationId = Guid.NewGuid();
    private readonly TestChanceRoller _chanceRoller = new() { Result = true };
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AttemptLockpickCommandHandler _handler = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _player = Builders.MakeCreature(_worldId, locationId: _exteriorLocationId);
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsMonitor<LockpickingOptions>>(
                new TestOptionsMonitor<LockpickingOptions>(new LockpickingOptions())
            )
            .AddSingleton<IOptionsMonitor<GuardEncounterOptions>>(
                new TestOptionsMonitor<GuardEncounterOptions>(new GuardEncounterOptions())
            )
            .AddSingleton<IChanceRoller>(_chanceRoller)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AttemptLockpickCommandHandler>();

        _context.Creatures.Add(_player);
        _context.Locations.Add(Builders.MakeLocation(worldId: _worldId, id: _exteriorLocationId));
        _context.GameSessions.Add(Builders.MakeGameSession(_worldId, _player.Id));
        _context.CreatureSkills.AddRange(
            Builders.MakeCreatureSkill(_player.Id, Skill.Lockpicking, level: 1, worldId: _worldId),
            Builders.MakeCreatureSkill(_player.Id, Skill.Sneak, level: 1, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsNothingToPick_WhenTheDoorIsNotLocked()
    {
        // Arrange
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connectorId, isLocked: false, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = _worldId,
                ConnectorId = connectorId,
                DestinationLocationId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(LockpickAttemptOutcome.NothingToPick, result.Outcome);
    }

    [Fact]
    public async Task Handle_OpensDoorAndIncrementsSkill_WhenRollSucceedsWithNoWitnesses()
    {
        // Arrange
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = _worldId,
                ConnectorId = connectorId,
                DestinationLocationId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(LockpickAttemptOutcome.Opened, result.Outcome);
        Assert.Null(result.GuardEncounter);
        Assert.Null(result.HostileEncounter);

        await using var verifyContext = db.CreateContext();
        var lockpicking = await verifyContext.CreatureSkills.FirstAsync(
            s => s.CreatureId == _player.Id && s.Skill == Skill.Lockpicking,
            TestContext.Current.CancellationToken
        );
        Assert.True(lockpicking.Experience > 0);
    }

    [Fact]
    public async Task Handle_LeavesDoorLocked_WhenRollFails()
    {
        // Arrange
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = false;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = _worldId,
                ConnectorId = connectorId,
                DestinationLocationId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(LockpickAttemptOutcome.Failed, result.Outcome);

        await using var verifyContext = db.CreateContext();
        var door = await verifyContext.DoorConnectors.FirstAsync(
            d => d.ConnectorId == connectorId,
            TestContext.Current.CancellationToken
        );
        Assert.True(door.IsLocked);
    }

    [Fact]
    public async Task Handle_StartsGuardEncounter_WhenAGuardSpotsTheExteriorPick()
    {
        // Arrange
        var guard = Builders.MakeCreature(
            _worldId,
            profession: Profession.Guard,
            locationId: _exteriorLocationId
        );
        var faction = Builders.MakeFaction(worldId: _worldId, isCityFaction: true);
        _context.Creatures.Add(guard);
        _context.Factions.Add(faction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(_worldId, faction.Id, guard.Id));
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = _worldId,
                ConnectorId = connectorId,
                DestinationLocationId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result.GuardEncounter);
        Assert.Null(result.HostileEncounter);
        Assert.Equal(guard.Id, result.GuardEncounter.GuardCreatureId);
        Assert.Equal(faction.Id, result.GuardEncounter.CityFactionId);
    }

    [Fact]
    public async Task Handle_StartsHostileEncounter_WhenAnOccupantSpotsAnInteriorPick()
    {
        // Arrange — the player already broke into this building earlier, and is now picking a
        // second, interior lock while still inside.
        var roomLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: _worldId, buildingType: BuildingType.House);
        var room = Builders.MakeRoom(building.Id, worldId: _worldId, locationId: roomLocationId);
        var location = Builders.MakeLocation(
            worldId: _worldId,
            id: roomLocationId,
            roomId: room.Id
        );
        var occupant = Builders.MakeCreature(_worldId, locationId: roomLocationId);
        var faction = Builders.MakeFaction(worldId: _worldId, isCityFaction: true);
        _player.LocationId = roomLocationId;
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _context.Creatures.Add(occupant);
        _context.Factions.Add(faction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(_worldId, faction.Id, occupant.Id));
        _context.GameSessions.Single(s => s.WorldId == _worldId).TrespassingBuildingId =
            building.Id;
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = _worldId,
                ConnectorId = connectorId,
                DestinationLocationId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result.GuardEncounter);
        Assert.NotNull(result.HostileEncounter);
        var member = Assert.Single(result.HostileEncounter.Members);
        Assert.Equal(occupant.Id, member.Id);
    }

    [Fact]
    public async Task Handle_DoesNotRecordCrimeOrFlagTrespassing_WhenTheBuildingIsOwnedByThePlayer()
    {
        // Arrange
        var destinationLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: _worldId, buildingType: BuildingType.House);
        var room = Builders.MakeRoom(
            building.Id,
            worldId: _worldId,
            locationId: destinationLocationId
        );
        var location = Builders.MakeLocation(
            worldId: _worldId,
            id: destinationLocationId,
            roomId: room.Id
        );
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _context.BuildingOwners.Add(Builders.MakeBuildingOwner(building.Id, _player.Id, _worldId));
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = _worldId,
                ConnectorId = connectorId,
                DestinationLocationId = destinationLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(LockpickAttemptOutcome.Opened, result.Outcome);
        Assert.Null(result.GuardEncounter);
        Assert.Null(result.HostileEncounter);

        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext
                .Crimes.OfType<BreakingAndEnteringCrime>()
                .AnyAsync(c => c.PlayerId == _player.Id, TestContext.Current.CancellationToken)
        );
        var session = await verifyContext.GameSessions.SingleAsync(
            s => s.WorldId == _worldId,
            TestContext.Current.CancellationToken
        );
        Assert.Null(session.TrespassingBuildingId);
    }

    [Fact]
    public async Task Handle_RecordsCrimeAndFlagsTrespassing_WhenTheDoorOpensIntoAnUnauthorizedBuildingWithNoWitnesses()
    {
        // Arrange — a clean, unwitnessed break-in should still leave a permanent record.
        var destinationLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: _worldId, buildingType: BuildingType.House);
        var room = Builders.MakeRoom(
            building.Id,
            worldId: _worldId,
            locationId: destinationLocationId
        );
        var location = Builders.MakeLocation(
            worldId: _worldId,
            id: destinationLocationId,
            roomId: room.Id
        );
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: _worldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = _worldId,
                ConnectorId = connectorId,
                DestinationLocationId = destinationLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(LockpickAttemptOutcome.Opened, result.Outcome);
        Assert.Null(result.GuardEncounter);
        Assert.Null(result.HostileEncounter);

        await using var verifyContext = db.CreateContext();
        var crime = await verifyContext
            .Crimes.OfType<BreakingAndEnteringCrime>()
            .SingleAsync(c => c.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.Equal(building.Id, crime.BuildingId);
        Assert.False(
            await verifyContext.CrimeWitnesses.AnyAsync(
                w => w.CrimeId == crime.Id,
                TestContext.Current.CancellationToken
            )
        );
        var session = await verifyContext.GameSessions.SingleAsync(
            s => s.WorldId == _worldId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(building.Id, session.TrespassingBuildingId);
    }

    [Fact]
    public async Task Handle_GrantsAHiddenUntradeableKey_WhenTheDoorOpens()
    {
        // Arrange — the door's raw IsLocked flag is schedule-owned and never touched by a pick, so
        // the key is the only thing that keeps a picked door passable afterward.
        var destinationLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: _worldId, buildingType: BuildingType.House);
        var room = Builders.MakeRoom(
            building.Id,
            worldId: _worldId,
            locationId: destinationLocationId
        );
        var location = Builders.MakeLocation(
            worldId: _worldId,
            id: destinationLocationId,
            roomId: room.Id
        );
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        var connectorId = Guid.NewGuid();
        var doorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(
                connectorId,
                isLocked: true,
                lockLevel: 1,
                worldId: _worldId,
                id: doorId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = _worldId,
                ConnectorId = connectorId,
                DestinationLocationId = destinationLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert — the key must be linked to the DoorConnector row's own Id, not its shared
        // ConnectorId, since that's what ResolveAccessibleConnectorsCommand looks keys up by.
        await using var verifyContext = db.CreateContext();
        var key = await verifyContext.Items.SingleAsync(
            i => i.Ownership.OwnerType == OwnerType.Creature && i.Ownership.OwnerId == _player.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(key.CanTrade);
        Assert.True(key.IsHidden);
        var doorConnectorKey = await verifyContext.DoorConnectorKeys.SingleAsync(
            k => k.DoorConnectorId == doorId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(key.Id, doorConnectorKey.ItemId);
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class TestChanceRoller : IChanceRoller
    {
        public bool Result { get; set; } = true;

        public bool Roll(float chance) => Result;
    }
}
