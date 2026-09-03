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
        _player = Builders.MakeCreature(WorldId, locationId: _roomLocationId);
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
    public async Task Handle_ReturnsNull_WhenPlayerOwnsTheBuilding()
    {
        // Arrange
        var occupant = Builders.MakeCreature(WorldId, locationId: _roomLocationId);
        _context.Creatures.Add(occupant);
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
    public async Task Handle_ReturnsNull_WhenNoOneIsPresent()
    {
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsHostileEncounterWithTheOccupant_WhenUnauthorizedAndDetected()
    {
        // Arrange
        var occupant = Builders.MakeCreature(WorldId, locationId: _roomLocationId);
        var faction = Builders.MakeFaction(worldId: WorldId, isCityFaction: true);
        _context.Creatures.Add(occupant);
        _context.Factions.Add(faction);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, faction.Id, occupant.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
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
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = false;

        // Act
        var result = await _handler.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
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
