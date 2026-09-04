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
public sealed class EvaluateTrespassingEncounterCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly TestChanceRoller _chanceRoller = new() { Result = true };
    private readonly Guid _buildingId = Guid.NewGuid();
    private readonly Guid _roomLocationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private EvaluateTrespassingEncounterCommandHandler _handler = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _player = Builders.MakeCreature(WorldId, locationId: _roomLocationId, isSneaking: true);
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsMonitor<LockpickingOptions>>(
                new TestOptionsMonitor<LockpickingOptions>(new LockpickingOptions())
            )
            .AddSingleton<IChanceRoller>(_chanceRoller)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<EvaluateTrespassingEncounterCommandHandler>();

        var building = Builders.MakeBuilding(
            worldId: WorldId,
            buildingType: BuildingType.House,
            id: _buildingId
        );
        var room = Builders.MakeRoom(_buildingId, worldId: WorldId, locationId: _roomLocationId);
        var location = Builders.MakeLocation(
            worldId: WorldId,
            id: _roomLocationId,
            roomId: room.Id
        );

        _context.Creatures.Add(_player);
        _context.Buildings.Add(building);
        _context.Rooms.Add(room);
        _context.Locations.Add(location);
        _context.GameSessions.Add(Builders.MakeGameSession(WorldId, _player.Id));
        _context.CreatureSkills.Add(
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
    public async Task Handle_ReturnsNull_WhenPlayerIsNotInsideAnyBuilding()
    {
        // Arrange
        _player.LocationId = Guid.NewGuid();
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenThePlayerOwnsTheBuilding()
    {
        // Arrange — owning the building means it can never be trespassing, regardless of history.
        await SeedBreakInCrime();
        await SeedFrontDoor(isLocked: true);
        _context.BuildingOwners.Add(Builders.MakeBuildingOwner(_buildingId, _player.Id, WorldId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenThePlayerHasNeverBrokenIntoThisBuilding()
    {
        // Arrange — an ordinary visit through a currently-locked door isn't suspicious on its own;
        // it only matters for a player who has actually forced entry here before.
        await SeedFrontDoor(isLocked: true);

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheFrontDoorIsCurrentlyUnlocked()
    {
        // Arrange — a known past break-in doesn't matter while the building is open to the public.
        var occupant = Builders.MakeCreature(WorldId, locationId: _roomLocationId);
        _context.Creatures.Add(occupant);
        await SeedBreakInCrime();
        await SeedFrontDoor(isLocked: false);

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoOneIsPresent()
    {
        // Arrange
        await SeedBreakInCrime();
        await SeedFrontDoor(isLocked: true);

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsHostileEncounterWithTheOccupant_WhenTrespassingAndDetected()
    {
        // Arrange
        var occupant = Builders.MakeCreature(WorldId, locationId: _roomLocationId);
        var faction = Builders.MakeFaction(worldId: WorldId, isCityFaction: true);
        _context.Creatures.Add(occupant);
        _context.Factions.Add(faction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, faction.Id, occupant.Id));
        await SeedBreakInCrime();
        await SeedFrontDoor(isLocked: true);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(faction.Id, result.FactionId);
        var member = Assert.Single(result.Members);
        Assert.Equal(occupant.Id, member.Id);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNotDetected()
    {
        // Arrange
        var occupant = Builders.MakeCreature(WorldId, locationId: _roomLocationId);
        _context.Creatures.Add(occupant);
        await SeedBreakInCrime();
        await SeedFrontDoor(isLocked: true);
        _chanceRoller.Result = false;

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
        await using var verifyContext = db.CreateContext();
        var sneak = await verifyContext.CreatureSkills.SingleAsync(
            s => s.CreatureId == _player.Id && s.Skill == Skill.Sneak,
            TestContext.Current.CancellationToken
        );
        Assert.True(sneak.Experience > 0);
    }

    [Fact]
    public async Task Handle_ReturnsHostileEncounter_WhenPlayerIsNotSneaking_RegardlessOfRoll()
    {
        // Arrange — no sneak stance means no chance to avoid detection, whatever the roll says.
        var occupant = Builders.MakeCreature(WorldId, locationId: _roomLocationId);
        var faction = Builders.MakeFaction(worldId: WorldId, isCityFaction: true);
        _context.Creatures.Add(occupant);
        _context.Factions.Add(faction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, faction.Id, occupant.Id));
        await SeedBreakInCrime();
        await SeedFrontDoor(isLocked: true);
        _player.IsSneaking = false;
        _chanceRoller.Result = false;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        var member = Assert.Single(result.Members);
        Assert.Equal(occupant.Id, member.Id);
    }

    private async Task SeedBreakInCrime()
    {
        _context.Crimes.Add(
            new BreakingAndEnteringCrime
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = _roomLocationId,
                BuildingId = _buildingId,
                BuildingName = "Test Building",
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedFrontDoor(bool isLocked)
    {
        var originLocationId = Guid.NewGuid();
        _context.Locations.Add(Builders.MakeLocation(worldId: WorldId, id: originLocationId));
        var connector = Builders.MakeLocationConnector(
            originLocationId,
            _roomLocationId,
            worldId: WorldId
        );
        _context.LocationConnectors.Add(connector);
        _context.DoorConnectors.Add(
            Builders.MakeDoorConnector(connector.Id, isLocked: isLocked, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
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
