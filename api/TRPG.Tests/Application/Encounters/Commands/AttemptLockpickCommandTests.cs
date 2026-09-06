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
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _exteriorLocationId = Guid.NewGuid();
    private readonly TestChanceRoller _chanceRoller = new() { Result = true };
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AttemptLockpickCommandHandler _handler = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _player = Builders.MakeCreature(WorldId, locationId: _exteriorLocationId, isSneaking: true);
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
        _context.Locations.Add(Builders.MakeLocation(worldId: WorldId, id: _exteriorLocationId));
        _context.GameSessions.Add(Builders.MakeGameSession(WorldId, _player.Id));
        _context.CreatureSkills.AddRange(
            Builders.MakeCreatureSkill(_player.Id, Skill.Lockpicking, level: 1, worldId: WorldId),
            Builders.MakeCreatureSkill(_player.Id, Skill.Sneak, level: 1, worldId: WorldId)
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
            Builders.MakeDoorConnector(connectorId, isLocked: false, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
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
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                ConnectorId = connectorId,
                DestinationLocationId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(LockpickAttemptOutcome.Opened, result.Outcome);
        Assert.Null(result.Encounter);

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
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = false;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
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
        var destinationLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.House);
        var room = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            locationId: destinationLocationId
        );
        var location = Builders.MakeLocation(
            worldId: WorldId,
            id: destinationLocationId,
            roomId: room.Id
        );
        var guard = Builders.MakeCreature(
            WorldId,
            profession: Profession.Guard,
            locationId: _exteriorLocationId
        );
        var faction = Builders.MakeFaction(worldId: WorldId, isCityFaction: true);
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _context.Creatures.Add(guard);
        _context.Factions.Add(faction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, faction.Id, guard.Id));
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                ConnectorId = connectorId,
                DestinationLocationId = destinationLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var guardEncounter = Assert.IsType<GuardEncounter>(result.Encounter);
        Assert.Equal(guard.Id, guardEncounter.GuardCreatureId);
        Assert.Equal(faction.Id, guardEncounter.CityFactionId);
    }

    [Fact]
    public async Task Handle_StartsGuardEncounter_WhenPlayerIsNotSneaking_RegardlessOfRoll()
    {
        // Arrange — no sneak stance means no chance to avoid detection, whatever the roll says.
        var destinationLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.House);
        var room = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            locationId: destinationLocationId
        );
        var location = Builders.MakeLocation(
            worldId: WorldId,
            id: destinationLocationId,
            roomId: room.Id
        );
        var guard = Builders.MakeCreature(
            WorldId,
            profession: Profession.Guard,
            locationId: _exteriorLocationId
        );
        var faction = Builders.MakeFaction(worldId: WorldId, isCityFaction: true);
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _context.Creatures.Add(guard);
        _context.Factions.Add(faction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, faction.Id, guard.Id));
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: WorldId)
        );
        _player.IsSneaking = false;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = false;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                ConnectorId = connectorId,
                DestinationLocationId = destinationLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var guardEncounter = Assert.IsType<GuardEncounter>(result.Encounter);
        Assert.Equal(guard.Id, guardEncounter.GuardCreatureId);
    }

    [Fact]
    public async Task Handle_StartsGuardEncounter_WhenPickingATimedLockToBreakOutOfJail()
    {
        // Arrange — a timed lock is only ever set by a jail sentence, and the guard station the
        // player is breaking out into is the destination, not the cell they're leaving
        var cellsLocationId = Guid.NewGuid();
        var guardStationLocationId = Guid.NewGuid();
        var faction = Builders.MakeFaction(worldId: WorldId, isCityFaction: true);
        var jail = Builders.MakeBuilding(
            worldId: WorldId,
            buildingType: BuildingType.Jail,
            factionId: faction.Id
        );
        var cellsRoom = Builders.MakeRoom(jail.Id, worldId: WorldId, locationId: cellsLocationId);
        var guardStationRoom = Builders.MakeRoom(
            jail.Id,
            worldId: WorldId,
            locationId: guardStationLocationId
        );
        var guard = Builders.MakeCreature(
            WorldId,
            profession: Profession.Guard,
            locationId: guardStationLocationId
        );
        _player.LocationId = cellsLocationId;
        _player.IsSneaking = false;
        _context.Buildings.Add(jail);
        _context.Rooms.AddRange(cellsRoom, guardStationRoom);
        _context.Locations.AddRange(
            Builders.MakeLocation(worldId: WorldId, id: cellsLocationId, roomId: cellsRoom.Id),
            Builders.MakeLocation(
                worldId: WorldId,
                id: guardStationLocationId,
                roomId: guardStationRoom.Id
            )
        );
        _context.Creatures.Add(guard);
        _context.Factions.Add(faction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, faction.Id, guard.Id));
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(
                connectorId,
                isLocked: true,
                lockLevel: 1,
                worldId: WorldId,
                unlocksAtPlaytime: TimeSpan.FromHours(99)
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                ConnectorId = connectorId,
                DestinationLocationId = guardStationLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var guardEncounter = Assert.IsType<GuardEncounter>(result.Encounter);
        Assert.Equal(guard.Id, guardEncounter.GuardCreatureId);
        Assert.NotNull(guardEncounter.TriggeringCrimeId);
    }

    [Fact]
    public async Task Handle_StartsHostileEncounter_WhenAnOccupantSpotsAnInteriorPick()
    {
        // Arrange — the player already broke into this building earlier, and is now picking a
        // second, interior lock while still inside.
        var roomLocationId = Guid.NewGuid();
        var faction = Builders.MakeFaction(worldId: WorldId, isCityFaction: true);
        var building = Builders.MakeBuilding(
            worldId: WorldId,
            buildingType: BuildingType.House,
            factionId: faction.Id
        );
        var room = Builders.MakeRoom(building.Id, worldId: WorldId, locationId: roomLocationId);
        var location = Builders.MakeLocation(worldId: WorldId, id: roomLocationId, roomId: room.Id);
        var occupant = Builders.MakeCreature(WorldId, locationId: roomLocationId);
        _player.LocationId = roomLocationId;
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _context.Creatures.Add(occupant);
        _context.Factions.Add(faction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, faction.Id, occupant.Id));
        _context.Crimes.Add(
            new LockpickingCrime
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = roomLocationId,
                BuildingId = building.Id,
                BuildingName = building.Name,
            }
        );
        var frontDoorOriginLocationId = Guid.NewGuid();
        _context.Locations.Add(
            Builders.MakeLocation(worldId: WorldId, id: frontDoorOriginLocationId)
        );
        var frontDoorConnector = Builders.MakeLocationConnector(
            frontDoorOriginLocationId,
            roomLocationId,
            worldId: WorldId
        );
        _context.LocationConnectors.Add(frontDoorConnector);
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(frontDoorConnector.Id, isLocked: true, worldId: WorldId)
        );
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                ConnectorId = connectorId,
                DestinationLocationId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var hostileEncounter = Assert.IsType<HostileEncounter>(result.Encounter);
        var member = Assert.Single(hostileEncounter.Members);
        Assert.Equal(occupant.Id, member.Id);
    }

    [Fact]
    public async Task Handle_DoesNotRecordCrime_WhenTheBuildingIsOwnedByThePlayer()
    {
        // Arrange
        var destinationLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.House);
        var room = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            locationId: destinationLocationId
        );
        var location = Builders.MakeLocation(
            worldId: WorldId,
            id: destinationLocationId,
            roomId: room.Id
        );
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _context.BuildingOwners.Add(Builders.MakeBuildingOwner(building.Id, _player.Id, WorldId));
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                ConnectorId = connectorId,
                DestinationLocationId = destinationLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(LockpickAttemptOutcome.Opened, result.Outcome);
        Assert.Null(result.Encounter);

        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext
                .Crimes.OfType<LockpickingCrime>()
                .AnyAsync(c => c.PlayerId == _player.Id, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_DoesNotStartAGuardEncounter_WhenAGuardWatchesTheOwnerPickTheirOwnDoor()
    {
        // Arrange — a guard witnessing the pick must not fabricate a crime against an authorized owner.
        var destinationLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.House);
        var room = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            locationId: destinationLocationId
        );
        var location = Builders.MakeLocation(
            worldId: WorldId,
            id: destinationLocationId,
            roomId: room.Id
        );
        var guard = Builders.MakeCreature(
            WorldId,
            profession: Profession.Guard,
            locationId: _exteriorLocationId
        );
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _context.Creatures.Add(guard);
        _context.BuildingOwners.Add(Builders.MakeBuildingOwner(building.Id, _player.Id, WorldId));
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                ConnectorId = connectorId,
                DestinationLocationId = destinationLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result.Encounter);

        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext
                .Crimes.OfType<LockpickingCrime>()
                .AnyAsync(c => c.PlayerId == _player.Id, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_RecordsCrime_WhenTheDoorOpensIntoAnUnauthorizedBuildingWithNoWitnesses()
    {
        // Arrange — a clean, unwitnessed break-in should still leave a permanent record.
        var destinationLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.House);
        var room = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            locationId: destinationLocationId
        );
        var location = Builders.MakeLocation(
            worldId: WorldId,
            id: destinationLocationId,
            roomId: room.Id
        );
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        var connectorId = Guid.NewGuid();
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connectorId, isLocked: true, lockLevel: 1, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new AttemptLockpickCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                ConnectorId = connectorId,
                DestinationLocationId = destinationLocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(LockpickAttemptOutcome.Opened, result.Outcome);
        Assert.Null(result.Encounter);

        await using var verifyContext = db.CreateContext();
        var crime = await verifyContext
            .Crimes.OfType<LockpickingCrime>()
            .SingleAsync(c => c.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.Equal(building.Id, crime.BuildingId);
        Assert.False(
            await verifyContext.CrimeWitnesses.AnyAsync(
                w => w.CrimeId == crime.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_GrantsAHiddenUntradeableKey_WhenTheDoorOpens()
    {
        // Arrange — the door's raw IsLocked flag is schedule-owned and never touched by a pick, so
        // the key is the only thing that keeps a picked door passable afterward.
        var destinationLocationId = Guid.NewGuid();
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: BuildingType.House);
        var room = Builders.MakeRoom(
            building.Id,
            worldId: WorldId,
            locationId: destinationLocationId
        );
        var location = Builders.MakeLocation(
            worldId: WorldId,
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
                worldId: WorldId,
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
                WorldId = WorldId,
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
